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
