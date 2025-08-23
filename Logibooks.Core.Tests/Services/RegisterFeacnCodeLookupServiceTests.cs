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
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE),
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class RegisterFeacnCodeLookupServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rfls_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static IServiceScopeFactory CreateMockScopeFactory(AppDbContext dbContext, IParcelFeacnCodeLookupService lookupService)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockServiceProvider.Setup(s => s.GetService(typeof(AppDbContext))).Returns(dbContext);
        mockServiceProvider.Setup(s => s.GetService(typeof(IParcelFeacnCodeLookupService))).Returns(lookupService);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        return mockScopeFactory.Object;
    }

    [Test]
    public async Task StartLookupAsync_RunsLookupForAllOrders()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 1, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1 },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1 });
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelFeacnCodeLookupService>();
        mock.Setup(m => m.LookupAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());

        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterFeacnCodeLookupService(ctx, scopeFactory, logger, new MorphologySearchService());

        var handle = await svc.StartLookupAsync(1);
        await Task.Delay(100);
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Total, Is.EqualTo(-1));
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Finished, Is.True);
        mock.Verify(m => m.LookupAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Cancel_StopsProcessing()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 2, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrParcel { Id = 3, RegisterId = 2, StatusId = 1 },
            new WbrParcel { Id = 4, RegisterId = 2, StatusId = 1 });
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelFeacnCodeLookupService>();
        mock.Setup(m => m.LookupAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(20); return new List<int>(); });

        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterFeacnCodeLookupService(ctx, scopeFactory, logger, new MorphologySearchService());

        var handle = await svc.StartLookupAsync(2);
        svc.Cancel(handle);
        await Task.Delay(100);
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Finished, Is.True);
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Total, Is.EqualTo(-1));
    }

    [Test]
    public async Task StartLookupAsync_ReturnsSameHandle_WhenAlreadyRunning()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 3, FileName = "r.xlsx" });
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelFeacnCodeLookupService>();
        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterFeacnCodeLookupService(ctx, scopeFactory, logger, new MorphologySearchService());

        var h1 = await svc.StartLookupAsync(3);
        var h2 = await svc.StartLookupAsync(3);
        bool f = svc.GetProgress(h1)?.Finished ?? false;
        if (!f) Assert.That(h1, Is.EqualTo(h2));
    }

    [Test]
    public async Task StartLookupAsync_SetsError_WhenScopedServicesNotResolved()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 10, FileName = "r.xlsx" });
        await ctx.SaveChangesAsync();

        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(null!);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IParcelFeacnCodeLookupService))).Returns(null!);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var svc = new RegisterFeacnCodeLookupService(ctx, mockScopeFactory.Object, logger, new MorphologySearchService());

        // Expect StartLookupAsync to throw an exception when services cannot be resolved
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.StartLookupAsync(10));
        Assert.That(ex?.Message ?? String.Empty, Is.EqualTo("Failed to resolve required services"));
    }

    [Test]
    public async Task StartLookupAsync_LookupsOrdersWithRealLookupService()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 100, FileName = "test.xlsx" });
        ctx.Orders.AddRange(
            new WbrParcel { Id = 101, RegisterId = 100, ProductName = "This is SPAM and malware" },
            new WbrParcel { Id = 102, RegisterId = 100, ProductName = "Clean product" }
        );
        ctx.KeyWords.AddRange(
            new KeyWord { Id = 201, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 201, FeacnCode = "1" }] },
            new KeyWord { Id = 202, Word = "malware", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 202, FeacnCode = "2" }] },
            new KeyWord { Id = 203, Word = "virus", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology, KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 203, FeacnCode = "3" }] }
        );
        await ctx.SaveChangesAsync();

        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var lookupService = new ParcelFeacnCodeLookupService(ctx, new MorphologySearchService());
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(ctx);
        mockServiceProvider.Setup(x => x.GetService(typeof(IParcelFeacnCodeLookupService))).Returns(lookupService);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var svc = new RegisterFeacnCodeLookupService(ctx, mockScopeFactory.Object, logger, new MorphologySearchService());

        var handle = await svc.StartLookupAsync(100);

        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            progress = svc.GetProgress(handle);
            if (progress != null && progress.Finished)
                break;
            await Task.Delay(50);
        }
        sw.Stop();

        var order1 = await ctx.Orders.Include(o => o.BaseOrderKeyWords).FirstAsync(o => o.Id == 101);
        var order2 = await ctx.Orders.Include(o => o.BaseOrderKeyWords).FirstAsync(o => o.Id == 102);

        var keyWordIds1 = order1.BaseOrderKeyWords.Select(l => l.KeyWordId).ToList();
        Assert.That(keyWordIds1, Does.Contain(201));
        Assert.That(keyWordIds1, Does.Contain(202));
        Assert.That(keyWordIds1.Count, Is.EqualTo(2));

        Assert.That(order2.BaseOrderKeyWords, Is.Empty);
    }

    [Test]
    public async Task StartLookupAsync_SkipsOrdersWithMarkedByPartnerOrGreaterCheckStatusId()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 200, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrParcel { Id = 201, RegisterId = 200, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 202, RegisterId = 200, StatusId = 1, CheckStatusId = 200 },
            new WbrParcel { Id = 203, RegisterId = 200, StatusId = 1, CheckStatusId = 301 },
            new WbrParcel { Id = 204, RegisterId = 200, StatusId = 1, CheckStatusId = 400 }
        );
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelFeacnCodeLookupService>();
        mock.Setup(m => m.LookupAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());

        var logger = new LoggerFactory().CreateLogger<RegisterFeacnCodeLookupService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterFeacnCodeLookupService(ctx, scopeFactory, logger, new MorphologySearchService());

        var handle = await svc.StartLookupAsync(200);
        await Task.Delay(100);

        mock.Verify(m => m.LookupAsync(
            It.Is<BaseParcel>(o => o.Id == 201),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        mock.Verify(m => m.LookupAsync(
            It.Is<BaseParcel>(o => o.Id == 202),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mock.Verify(m => m.LookupAsync(
            It.Is<BaseParcel>(o => o.Id == 203),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mock.Verify(m => m.LookupAsync(
            It.Is<BaseParcel>(o => o.Id == 204),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

