// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Interfaces;
using Quartz;

namespace Logibooks.Core.Services;

public class UpdateCountriesJob(IUpdateCountriesService service, ILogger<UpdateCountriesJob> logger) : IJob
{
    private readonly IUpdateCountriesService _service = service;
    private readonly ILogger<UpdateCountriesJob> _logger = logger;

    private static CancellationTokenSource? _prev;
    private static readonly object _lock = new();

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _prev?.Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            _prev = cts;
        }

        _logger.LogInformation("Executing UpdateCountriesJob");
        try
        {
            await _service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateCountriesJob was cancelled");
        }
        finally
        {
            cts.Dispose();
            lock (_lock)
            {
                if (_prev == cts) _prev = null;
            }
        }
    }
}
