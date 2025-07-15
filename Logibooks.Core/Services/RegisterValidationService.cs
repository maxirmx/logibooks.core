using System.Collections.Concurrent;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class RegisterValidationService(AppDbContext db, IOrderValidationService orderSvc, ILogger<RegisterValidationService> logger) : IRegisterValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IOrderValidationService _orderSvc = orderSvc;
    private readonly ILogger<RegisterValidationService> _logger = logger;

    private class ValidationProcess
    {
        public Guid HandleId { get; } = Guid.NewGuid();
        public int RegisterId { get; }
        public int Total { get; set; }
        public int Processed;
        public bool Completed;
        public bool Canceled;
        public string? Error;
        public CancellationTokenSource Cts { get; } = new();
        public ValidationProcess(int regId) { RegisterId = regId; }
    }

    private readonly ConcurrentDictionary<int, ValidationProcess> _byRegister = new();
    private readonly ConcurrentDictionary<Guid, ValidationProcess> _byHandle = new();

    public async Task<Guid> StartValidationAsync(int registerId, CancellationToken cancellationToken = default)
    {
        if (_byRegister.TryGetValue(registerId, out var existing))
        {
            return existing.HandleId;
        }

        var orders = await _db.Orders
            .Where(o => o.RegisterId == registerId)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var process = new ValidationProcess(registerId) { Total = orders.Count };
        if (!_byRegister.TryAdd(registerId, process))
        {
            return _byRegister[registerId].HandleId;
        }
        _byHandle[process.HandleId] = process;

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var id in orders)
                {
                    if (process.Cts.IsCancellationRequested)
                    {
                        process.Canceled = true;
                        break;
                    }
                    var order = await _db.Orders.FindAsync([id], cancellationToken: process.Cts.Token);
                    if (order != null)
                    {
                        await _orderSvc.ValidateAsync(order, process.Cts.Token);
                    }
                    process.Processed++;
                }
                if (!process.Canceled)
                    process.Completed = true;
            }
            catch (Exception ex)
            {
                process.Error = ex.Message;
                _logger.LogError(ex, "Register validation failed");
            }
            finally
            {
                _byRegister.TryRemove(registerId, out _);
            }
        });

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
                Completed = proc.Completed,
                Canceled = proc.Canceled,
                Error = proc.Error
            };
        }
        return null;
    }

    public bool CancelValidation(Guid handleId)
    {
        if (_byHandle.TryGetValue(handleId, out var proc))
        {
            proc.Cts.Cancel();
            proc.Canceled = true;
            return true;
        }
        return false;
    }
}
