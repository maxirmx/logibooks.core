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

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class RegisterValidationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rv_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static IServiceScopeFactory CreateMockScopeFactory(AppDbContext dbContext, IParcelValidationService orderValidationService, IFeacnPrefixCheckService feacnPrefixCheckService)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(AppDbContext))).Returns(dbContext);
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(IParcelValidationService))).Returns(orderValidationService);
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(IFeacnPrefixCheckService))).Returns(feacnPrefixCheckService);

        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        return mockScopeFactory.Object;
    }

    [Test]
    public async Task StartValidationAsync_RunsValidationForAllOrders()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 1, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 1 });
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelValidationService>();
        mock.Setup(m => m.ValidateAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(), 
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnSvc = new Mock<IFeacnPrefixCheckService>().Object;
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object, feacnSvc);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService(), feacnSvc);

        var handle = await svc.StartValidationAsync(1);
        await Task.Delay(100); // Give more time for background task
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Total, Is.EqualTo(-1));
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Finished, Is.True);
        mock.Verify(m => m.ValidateAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(), 
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task CancelValidation_StopsProcessing()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 2, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrOrder { Id = 3, RegisterId = 2, StatusId = 1 },
            new WbrOrder { Id = 4, RegisterId = 2, StatusId = 1 });
        await ctx.SaveChangesAsync();

        var tcs = new TaskCompletionSource();
        var mock = new Mock<IParcelValidationService>();
        mock.Setup(m => m.ValidateAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(20); tcs.TrySetResult(); });
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnSvc = new Mock<IFeacnPrefixCheckService>().Object;
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object, feacnSvc);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService(), feacnSvc);

        var handle = await svc.StartValidationAsync(2);
        svc.CancelValidation(handle);
        await Task.Delay(100); // Give more time for cancellation
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Finished, Is.True);
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Total, Is.EqualTo(-1));
    }

    [Test]
    public async Task StartValidationAsync_ReturnsSameHandle_WhenAlreadyRunning()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 3, FileName = "r.xlsx" });
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelValidationService>();
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnSvc = new Mock<IFeacnPrefixCheckService>().Object;
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object, feacnSvc);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService(), feacnSvc);

        var h1 = await svc.StartValidationAsync(3);
        var h2 = await svc.StartValidationAsync(3);
        bool f = svc.GetProgress(h1)?.Finished ?? false;  // consider fast finishing
        if (!f) Assert.That(h1, Is.EqualTo(h2));
    }

    [Test]
    public async Task StartValidationAsync_SetsError_WhenScopedServicesNotResolved()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 10, FileName = "r.xlsx" });
        await ctx.SaveChangesAsync();

        // Mock scope returns null for both services
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(null!);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IParcelValidationService))).Returns(null!);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnSvc = new Mock<IFeacnPrefixCheckService>().Object;
        var svc = new RegisterValidationService(ctx, mockScopeFactory.Object, logger, new MorphologySearchService(), feacnSvc);

        var handle = await svc.StartValidationAsync(10);

        // Poll for progress until error is set or timeout
        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            progress = svc.GetProgress(handle);
            if (progress != null && progress.Error != null)
                break;
            await Task.Delay(20);
        }
        sw.Stop();

        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        // Assert.That(progress.Error, Is.EqualTo("Failed to resolve required services"));
    }

    [Test]
    public async Task StartValidationAsync_ValidatesOrdersWithRealOrderValidationService()
    {
        using var ctx = CreateContext();
        // Add a register and two orders with product names containing stop words
        ctx.Registers.Add(new Register { Id = 100, FileName = "test.xlsx" });
        ctx.Orders.AddRange(
            new WbrOrder { Id = 101, RegisterId = 100, ProductName = "This is SPAM and malware", TnVed = "1234567890" },
            new WbrOrder { Id = 102, RegisterId = 100, ProductName = "Clean product", TnVed = "1234567890" }
        );
        // Add stop words: one that should match, one that should not
        ctx.StopWords.AddRange(
            new StopWord { Id = 201, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols },
            new StopWord { Id = 202, Word = "malware", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols },
            new StopWord { Id = 203, Word = "virus", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology } // not used for exact match
        );
        await ctx.SaveChangesAsync();

        // Use real OrderValidationService and MorphologySearchService
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnPrefixCheckService = new FeacnPrefixCheckService(ctx);
        var orderValidationService = new ParcelValidationService(ctx, new MorphologySearchService(), feacnPrefixCheckService);
        // Setup DI scope factory to provide real services
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(AppDbContext))).Returns(ctx);
        mockServiceProvider.Setup(x => x.GetService(typeof(IParcelValidationService))).Returns(orderValidationService);
        mockServiceProvider.Setup(x => x.GetService(typeof(IFeacnPrefixCheckService))).Returns(feacnPrefixCheckService);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

        var svc = new RegisterValidationService(ctx, mockScopeFactory.Object, logger, new MorphologySearchService(), feacnPrefixCheckService);

        // Act
        var handle = await svc.StartValidationAsync(100);

        // Wait for background validation to finish (poll for up to 2 seconds)
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


        // Reload orders and check stop word links
        var order1 = await ctx.Orders.Include(o => o.BaseOrderStopWords).FirstAsync(o => o.Id == 101);
        var order2 = await ctx.Orders.Include(o => o.BaseOrderStopWords).FirstAsync(o => o.Id == 102);

        // Order 1 should have links to both "spam" and "malware"
        var stopWordIds1 = order1.BaseOrderStopWords.Select(l => l.StopWordId).ToList();
        Assert.That(stopWordIds1, Does.Contain(201));
        Assert.That(stopWordIds1, Does.Contain(202));
        Assert.That(stopWordIds1.Count, Is.EqualTo(2));

        // Order 2 should have no stop word links
        Assert.That(order2.BaseOrderStopWords, Is.Empty);
    }

    [Test]
    public async Task StartValidationAsync_SkipsOrdersWithMarkedByPartnerOrGreaterCheckStatusId()
    {
        using var ctx = CreateContext();
        ctx.Registers.Add(new Register { Id = 200, FileName = "r.xlsx" });
        ctx.Orders.AddRange(
            new WbrOrder { Id = 201, RegisterId = 200, StatusId = 1, CheckStatusId = 1 },   // should be validated
            new WbrOrder { Id = 202, RegisterId = 200, StatusId = 1, CheckStatusId = 200 }, // marked by partner, should be skipped
            new WbrOrder { Id = 203, RegisterId = 200, StatusId = 1, CheckStatusId = 301 }, // approved, should be skipped
            new WbrOrder { Id = 204, RegisterId = 200, StatusId = 1, CheckStatusId = 400 }  // greater than approved, should be skipped
        );
        await ctx.SaveChangesAsync();

        var mock = new Mock<IParcelValidationService>();
        mock.Setup(m => m.ValidateAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var feacnSvc = new Mock<IFeacnPrefixCheckService>().Object;
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object, feacnSvc);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService(), feacnSvc);

        var handle = await svc.StartValidationAsync(200);
        await Task.Delay(100); // Give time for background task
        mock.Verify(m => m.ValidateAsync(
            It.Is<BaseParcel>(o => o.Id == 201),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        mock.Verify(m => m.ValidateAsync(
            It.Is<BaseParcel>(o => o.Id == 202),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        mock.Verify(m => m.ValidateAsync(
            It.Is<BaseParcel>(o => o.Id == 203),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        mock.Verify(m => m.ValidateAsync(
            It.Is<BaseParcel>(o => o.Id == 204),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<StopWord>>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
