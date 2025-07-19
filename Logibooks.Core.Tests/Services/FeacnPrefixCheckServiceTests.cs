using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class FeacnPrefixCheckServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fp_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public async Task CheckOrderAsync_InvalidTnVed_SetsStatus102()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "123" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(ctx);
        await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(102));
        Assert.That(ctx.Set<BaseOrderFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task CheckOrderAsync_MatchesPrefix_AddsLinkAndSetsStatus101()
    {
        using var ctx = CreateContext();
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", FeacnOrderId = 1 };
        ctx.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(ctx);
        await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(101));
        var link = ctx.Set<BaseOrderFeacnPrefix>().Single();
        Assert.That(link.BaseOrderId, Is.EqualTo(1));
        Assert.That(link.FeacnPrefixId, Is.EqualTo(10));
    }

    [Test]
    public async Task CheckOrderAsync_NoMatch_SetsStatus201()
    {
        using var ctx = CreateContext();
        ctx.FeacnPrefixes.Add(new FeacnPrefix { Id = 10, Code = "9999", FeacnOrderId = 1 });
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(ctx);
        await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
        Assert.That(ctx.Set<BaseOrderFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task CheckOrderAsync_ExceptionPreventsMatch()
    {
        using var ctx = CreateContext();
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", IntervalCode = "56", FeacnOrderId = 1 };
        prefix.FeacnPrefixExceptions.Add(new FeacnPrefixException { Id = 20, Code = "123455" });
        ctx.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234550000" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(ctx);
        await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
        Assert.That(ctx.Set<BaseOrderFeacnPrefix>().Any(), Is.False);
    }
}
