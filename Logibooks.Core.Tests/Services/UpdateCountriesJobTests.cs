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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Quartz;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

public class DummyUpdateService : IUpdateCountriesService
{
    public List<CancellationToken> Tokens { get; } = new();
    public TaskCompletionSource Started { get; } = new();
    public TaskCompletionSource Cancelled { get; } = new();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Tokens.Add(cancellationToken);
        Started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Cancelled.TrySetResult();
            throw;
        }
    }
}

[TestFixture]
public class UpdateCountriesJobTests
{
    [Test]
    public async Task Execute_CancelsPreviousJob()
    {
        var service = new DummyUpdateService();
        var job1 = new UpdateCountriesJob(service, NullLogger<UpdateCountriesJob>.Instance);
        var ctx1 = new Mock<IJobExecutionContext>();
        ctx1.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        var task1 = job1.Execute(ctx1.Object);
        await service.Started.Task; // first started

        var job2 = new UpdateCountriesJob(service, NullLogger<UpdateCountriesJob>.Instance);
        var cts2 = new CancellationTokenSource();
        var ctx2 = new Mock<IJobExecutionContext>();
        ctx2.Setup(c => c.CancellationToken).Returns(cts2.Token);
        var task2 = job2.Execute(ctx2.Object);
        await service.Cancelled.Task; // first cancelled by second start
        Assert.That(service.Tokens[0].IsCancellationRequested, Is.True);
        cts2.Cancel();
        try { await task2; } catch { }
        try { await task1; } catch { }
    }
}
