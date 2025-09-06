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

public class RegisterFeacnCodeLookupService(
    AppDbContext db,
    IServiceScopeFactory scopeFactory,
    ILogger<RegisterFeacnCodeLookupService> logger,
    IMorphologySearchService morphologyService) : IRegisterFeacnCodeLookupService
{
    private readonly AppDbContext _db = db;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<RegisterFeacnCodeLookupService> _logger = logger;
    private readonly IMorphologySearchService _morphologyService = morphologyService;

    private class LookupProcess
    {
        public Guid HandleId { get; } = Guid.NewGuid();
        public int RegisterId { get; }
        public int Total { get; set; }
        public int Processed;
        public bool Finished;
        public string? Error;
        public CancellationTokenSource Cts { get; } = new();
        public LookupProcess(int regId) { RegisterId = regId; }
    }

    private static readonly ConcurrentDictionary<int, LookupProcess> _byRegister = new();
    private static readonly ConcurrentDictionary<Guid, LookupProcess> _byHandle = new();

    public async Task<Guid> StartLookupAsync(int registerId, CancellationToken cancellationToken = default)
    {
        var process = new LookupProcess(registerId);
        if (!_byRegister.TryAdd(registerId, process))
        {
            return _byRegister[registerId].HandleId;
        }
        _byHandle[process.HandleId] = process;

        var allKeyWords = await _db.KeyWords.AsNoTracking().ToListAsync(cancellationToken);
        var morphologyContext = _morphologyService.InitializeContext(
            allKeyWords.Where(k => k.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes)
                .Select(k => new StopWord { Id = k.Id, Word = k.Word, MatchTypeId = k.MatchTypeId }));

        var tcs = new TaskCompletionSource();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
            var scopedLookupSvc = scope.ServiceProvider.GetService(typeof(IParcelFeacnCodeLookupService)) as IParcelFeacnCodeLookupService;

            if (scopedDb == null || scopedLookupSvc == null)
            {
                process.Error = "Failed to resolve required services";
                process.Finished = true;
                tcs.TrySetException(new InvalidOperationException("Failed to resolve required services"));
                CleanupProcess(registerId, process.HandleId);
                return;
            }

            try
            {
                var orders = await scopedDb.Parcels
                    .Where(o => o.RegisterId == registerId &&
                                o.CheckStatusId < (int)ParcelCheckStatusCode.Approved &&
                                o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner)
                    .Select(o => o.Id)
                    .ToListAsync(process.Cts.Token);
                process.Total = orders.Count;
                tcs.TrySetResult(); // Only set result after successful initialization

                var wordsLookupContext = new WordsLookupContext<KeyWord>(
                    allKeyWords.Where(k => k.MatchTypeId < (int)WordMatchTypeCode.MorphologyMatchTypes));

                foreach (var id in orders)
                {
                    if (process.Cts.IsCancellationRequested)
                    {
                        process.Finished = true;
                        break;
                    }
                    var order = await scopedDb.Parcels.FindAsync([id], cancellationToken: process.Cts.Token);
                    if (order != null)
                    {
                        await scopedLookupSvc.LookupAsync(order, morphologyContext, wordsLookupContext, process.Cts.Token);
                    }
                    process.Processed++;
                }
            }
            catch (Exception ex)
            {
                process.Error = ex.Message;
                _logger.LogError(ex, "Register feacn code lookup failed");
                tcs.TrySetException(ex);
            }
            finally
            {
                process.Finished = true;
                CleanupProcess(registerId, process.HandleId);
            }
        });

        await tcs.Task; 
        return process.HandleId;
    }

    private void CleanupProcess(int registerId, Guid handleId)
    {
        _byRegister.TryRemove(registerId, out _);
        _byHandle.TryRemove(handleId, out _);
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

