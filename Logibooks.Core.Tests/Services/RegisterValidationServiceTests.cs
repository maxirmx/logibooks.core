using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.RestModels;

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

    private static IServiceScopeFactory CreateMockScopeFactory(AppDbContext dbContext, IOrderValidationService orderValidationService)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(AppDbContext))).Returns(dbContext);
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(IOrderValidationService))).Returns(orderValidationService);

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

        var mock = new Mock<IOrderValidationService>();
        mock.Setup(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<MorphologyContext?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService());

        var handle = await svc.StartValidationAsync(1);
        await Task.Delay(100); // Give more time for background task
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Total, Is.EqualTo(-1));
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Finished, Is.True);
        mock.Verify(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<MorphologyContext?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        var mock = new Mock<IOrderValidationService>();
        mock.Setup(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<MorphologyContext?>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(20); tcs.TrySetResult(); });
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService());

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

        var mock = new Mock<IOrderValidationService>();
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var scopeFactory = CreateMockScopeFactory(ctx, mock.Object);
        var svc = new RegisterValidationService(ctx, scopeFactory, logger, new MorphologySearchService());

        var h1 = await svc.StartValidationAsync(3);
        var h2 = await svc.StartValidationAsync(3);

        Assert.That(h1, Is.EqualTo(h2));
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
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IOrderValidationService))).Returns(null!);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var svc = new RegisterValidationService(ctx, mockScopeFactory.Object, logger, new MorphologySearchService());

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
        Assert.That(progress.Error, Is.EqualTo("Failed to resolve required services"));
    }
}
