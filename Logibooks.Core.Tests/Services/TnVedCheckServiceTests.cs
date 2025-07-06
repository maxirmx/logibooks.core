using System.Threading.Tasks;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class TnVedCheckServiceTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tnved_service_db_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public async Task CheckOrder_SetsStatus101_WhenNoException()
    {
        using var ctx = CreateContext();
        ctx.AltaItems.Add(new AltaItem { Code = "123" });
        ctx.AltaExceptions.Add(new AltaException { Code = "12345" });
        ctx.Orders.Add(new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "1236" });
        await ctx.SaveChangesAsync();

        var svc = new TnVedCheckService(ctx);
        await svc.CheckOrder(1);

        var order = await ctx.Orders.FindAsync(1);
        Assert.That(order!.StatusId, Is.EqualTo(101));
    }

    [Test]
    public async Task CheckOrder_SetsStatus201_WhenExceptionMatches()
    {
        using var ctx = CreateContext();
        ctx.AltaItems.Add(new AltaItem { Code = "123" });
        ctx.AltaExceptions.Add(new AltaException { Code = "1234" });
        ctx.Orders.Add(new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "123456" });
        await ctx.SaveChangesAsync();

        var svc = new TnVedCheckService(ctx);
        await svc.CheckOrder(1);

        var order = await ctx.Orders.FindAsync(1);
        Assert.That(order!.StatusId, Is.EqualTo(201));
    }

    [Test]
    public async Task CheckOrder_SetsStatus201_WhenNoItemMatch()
    {
        using var ctx = CreateContext();
        ctx.AltaItems.Add(new AltaItem { Code = "999" });
        ctx.Orders.Add(new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "123456" });
        await ctx.SaveChangesAsync();

        var svc = new TnVedCheckService(ctx);
        await svc.CheckOrder(1);

        var order = await ctx.Orders.FindAsync(1);
        Assert.That(order!.StatusId, Is.EqualTo(201));
    }
}
