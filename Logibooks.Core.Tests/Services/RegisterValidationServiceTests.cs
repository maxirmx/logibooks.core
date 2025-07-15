using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

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
        mock.Setup(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var svc = new RegisterValidationService(ctx, mock.Object, logger);

        var handle = await svc.StartValidationAsync(1);
        await Task.Delay(50);
        var progress = svc.GetProgress(handle)!;

        Assert.That(progress.Total, Is.EqualTo(-1));
        Assert.That(progress.Processed, Is.EqualTo(-1));
        Assert.That(progress.Finished, Is.True);
        mock.Verify(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        mock.Setup(m => m.ValidateAsync(It.IsAny<BaseOrder>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await Task.Delay(20); tcs.TrySetResult(); });
        var logger = new LoggerFactory().CreateLogger<RegisterValidationService>();
        var svc = new RegisterValidationService(ctx, mock.Object, logger);

        var handle = await svc.StartValidationAsync(2);
        svc.CancelValidation(handle);
        await Task.Delay(50);
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
        var svc = new RegisterValidationService(ctx, mock.Object, logger);

        var h1 = await svc.StartValidationAsync(3);
        var h2 = await svc.StartValidationAsync(3);

        Assert.That(h1, Is.EqualTo(h2));
    }
}
