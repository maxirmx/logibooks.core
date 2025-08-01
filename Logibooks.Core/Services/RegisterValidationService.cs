// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

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

    private class ValidationProcess
    {
        public Guid HandleId { get; } = Guid.NewGuid();
        public int RegisterId { get; }
        public int Total { get; set; }
        public int Processed;
        public bool Finished;
        public string? Error;
        public CancellationTokenSource Cts { get; } = new();
        public ValidationProcess(int regId) { RegisterId = regId; }
    }

    private static readonly ConcurrentDictionary<int, ValidationProcess> _byRegister = new();
    private static readonly ConcurrentDictionary<Guid, ValidationProcess> _byHandle = new();

    public async Task<Guid> StartValidationAsync(int registerId, CancellationToken cancellationToken = default)
    {    
        var process = new ValidationProcess(registerId);
        if (!_byRegister.TryAdd(registerId, process))
        {
            return _byRegister[registerId].HandleId;
        }
        _byHandle[process.HandleId] = process;

        var allStopWords = await _db.StopWords.AsNoTracking().ToListAsync(cancellationToken);
        var morphologyContext = _morphologyService.InitializeContext(
            allStopWords.Where(sw => sw.MatchTypeId >= (int)StopWordMatchTypeCode.MorphologyMatchTypes));

        var tcs = new TaskCompletionSource();

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetService(typeof(AppDbContext)) as AppDbContext;
            var scopedOrderSvc = scope.ServiceProvider.GetService(typeof(IOrderValidationService)) as IOrderValidationService;
            var scopedFeacnSvc = scope.ServiceProvider.GetService(typeof(IFeacnPrefixCheckService)) as IFeacnPrefixCheckService;

            if (scopedDb == null || scopedOrderSvc == null || scopedFeacnSvc == null)
            {
                process.Error = "Failed to resolve required services";
                process.Finished = true;
                tcs.TrySetResult();
                _byRegister.TryRemove(registerId, out _);
                _byHandle.TryRemove(process.HandleId, out _);
                return;
            }

            try
            {
                var orders = await scopedDb.Orders
                    .Where(o => o.RegisterId == registerId && o.CheckStatusId < (int)OrderCheckStatusCode.Approved)
                    .Select(o => o.Id)
                    .ToListAsync(process.Cts.Token);
                process.Total = orders.Count;
                tcs.TrySetResult();

                var stopWordsContext = scopedOrderSvc.InitializeStopWordsContext(allStopWords);
                var feacnContext = await scopedFeacnSvc.CreateContext(process.Cts.Token);

                foreach (var id in orders)
                {
                    if (process.Cts.IsCancellationRequested)
                    {
                        process.Finished = true;
                        break;
                    }
                    var order = await scopedDb.Orders.FindAsync([id], cancellationToken: process.Cts.Token);
                    if (order != null)
                    {
                        await scopedOrderSvc.ValidateAsync(order, morphologyContext, stopWordsContext, feacnContext, process.Cts.Token);
                    }
                    process.Processed++;
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetResult();
                process.Error = ex.Message;
                _logger.LogError(ex, "Register validation failed");
            }
            finally
            {
                process.Finished = true;
                _byRegister.TryRemove(registerId, out _);
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

    public bool CancelValidation(Guid handleId)
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
