// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Services;

public class RegisterValidationService(
    AppDbContext db,
    IServiceScopeFactory scopeFactory,
    ILogger<RegisterValidationService> logger,
    IMorphologySearchService morphologyService,
    IFeacnPrefixCheckService feacnPrefixCheckService) : IRegisterValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<RegisterValidationService> _logger = logger;
    private readonly IMorphologySearchService _morphologyService = morphologyService;
    private readonly IFeacnPrefixCheckService _feacnPrefixCheckService = feacnPrefixCheckService;

    private enum ValidationKind
    {
        Kw,
        Feacn
    }

    private class ValidationProcess
    {
        public Guid HandleId { get; } = Guid.NewGuid();
        public int RegisterId { get; }
        public int Total { get; set; }
        public int Processed;
        public bool Finished;
        public string? Error;
        public CancellationTokenSource Cts { get; } = new();
        public ValidationKind Kind { get; }
        public ValidationProcess(int regId, ValidationKind kind)
        {
            RegisterId = regId;
            Kind = kind;
        }
    }

    private static readonly ConcurrentDictionary<int, ValidationProcess> _byRegister = new();
    private static readonly ConcurrentDictionary<Guid, ValidationProcess> _byHandle = new();

    public async Task<Guid> StartKwValidationAsync(int registerId, CancellationToken cancellationToken = default)
    {
        var process = new ValidationProcess(registerId, ValidationKind.Kw);
        if (!_byRegister.TryAdd(registerId, process))
        {
            var existing = _byRegister[registerId];
            if (existing.Kind != ValidationKind.Kw)
            {
                throw new InvalidOperationException("Different validation already running");
            }
            return existing.HandleId;
        }
        _byHandle[process.HandleId] = process;

        var allStopWords = await _db.StopWords.AsNoTracking().ToListAsync(cancellationToken);
        var morphologyContext = _morphologyService.InitializeContext(
            allStopWords.Where(sw => sw.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes));

        return await ExecuteValidationAsync(process, async (scopedDb, scopedValidationSvc, parcels, serviceProvider) =>
        {
            var stopWordsContext = new WordsLookupContext<StopWord>(allStopWords);

            foreach (var id in parcels)
            {
                if (process.Cts.IsCancellationRequested)
                {
                    process.Finished = true;
                    break;
                }
                var parcel = await scopedDb.Parcels.FindAsync(new object[] { id }, process.Cts.Token);
                if (parcel != null)
                {
                    await scopedValidationSvc.ValidateSwAsync(scopedDb, parcel, morphologyContext, stopWordsContext, process.Cts.Token);
                }
                process.Processed++;
            }
        }, "Register KW validation failed");
    }

    public async Task<Guid> StartFeacnValidationAsync(int registerId, CancellationToken cancellationToken = default)
    {
        var process = new ValidationProcess(registerId, ValidationKind.Feacn);
        if (!_byRegister.TryAdd(registerId, process))
        {
            var existing = _byRegister[registerId];
            if (existing.Kind != ValidationKind.Feacn)
            {
                throw new InvalidOperationException("Different validation already running");
            }
            return existing.HandleId;
        }
        _byHandle[process.HandleId] = process;

        return await ExecuteValidationAsync(process, async (scopedDb, scopedValidationSvc, parcels, serviceProvider) =>
        {
            var scopedFeacnSvc = serviceProvider.GetService(typeof(IFeacnPrefixCheckService)) as IFeacnPrefixCheckService ?? 
                                 throw new InvalidOperationException("Failed to resolve IFeacnPrefixCheckService");
            var feacnContext = await scopedFeacnSvc.CreateContext(process.Cts.Token);

            foreach (var id in parcels)
            {
                if (process.Cts.IsCancellationRequested)
                {
                    process.Finished = true;
                    break;
                }
                var parcel = await scopedDb.Parcels.FindAsync(new object[] { id }, process.Cts.Token);
                if (parcel != null)
                {
                    await scopedValidationSvc.ValidateFeacnAsync(scopedDb, parcel, feacnContext, process.Cts.Token);
                }
                process.Processed++;
            }
        }, "Register FEACN validation failed");
    }

    private async Task<Guid> ExecuteValidationAsync(
        ValidationProcess process,
        Func<AppDbContext, IParcelValidationService, List<int>, IServiceProvider, Task> validationAction,
        string errorMessage)
    {
        var tcs = new TaskCompletionSource();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
            var scopedValidationSvc = scope.ServiceProvider.GetService(typeof(IParcelValidationService)) as IParcelValidationService;

            if (scopedDb == null || scopedValidationSvc == null)
            {
                process.Error = "Failed to resolve required services";
                process.Finished = true;
                tcs.TrySetResult();
                _byRegister.TryRemove(process.RegisterId, out _);
                _byHandle.TryRemove(process.HandleId, out _);
                return;
            }

            try
            {
                var parcels = await scopedDb.Parcels
                    .Where(o => o.RegisterId == process.RegisterId &&
                                o.CheckStatusId < (int)ParcelCheckStatusCode.Approved &&
                                o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner)
                    .Select(o => o.Id)
                    .ToListAsync(process.Cts.Token);
                process.Total = parcels.Count;
                tcs.TrySetResult();

                await validationAction(scopedDb, scopedValidationSvc, parcels, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                tcs.TrySetResult();
                process.Error = ex.Message;
                _logger.LogError(ex, "{ErrorMessage}", errorMessage);

            }
            finally
            {
                process.Finished = true;
                _byRegister.TryRemove(process.RegisterId, out _);
                _byHandle.TryRemove(process.HandleId, out _);
            }
        });

        await tcs.Task;
        return process.HandleId;
    }

    public ValidationProgress? GetProgress(Guid handleId)
    {
        if (_byHandle.TryGetValue(handleId, out var proc))
        {
            return new ValidationProgress
            {
                HandleId = handleId,
                Total = proc.Total,
                Processed = proc.Processed,
                Finished = proc.Finished,
                Error = proc.Error
            };
        }
        return new ValidationProgress
        {
            HandleId = handleId,
            Total = -1,
            Processed = -1,
            Finished = true,
            Error = null
        };
    }

    public bool Cancel(Guid handleId)
    {
        if (_byHandle.TryGetValue(handleId, out var proc))
        {
            proc.Cts.Cancel();
            proc.Finished = true;
            return true;
        }
        return false;
    }
}
