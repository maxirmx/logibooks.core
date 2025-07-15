using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class OrderValidationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ov_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public async Task ValidateAsync_AddsLinksAndUpdatesStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, Description = "This is SPAM" };
        ctx.Orders.Add(order);
        ctx.StopWord.AddRange(
            new StopWord { Id = 2, Word = "spam", ExactMatch = true },
            new StopWord { Id = 3, Word = "other", ExactMatch = true }
        );
        ctx.Set<BaseOrderStopWord>().Add(new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx);
        await svc.ValidateAsync(1);

        Assert.That(ctx.Set<BaseOrderStopWord>().Count(), Is.EqualTo(1));
        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(2));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(201));
    }

    [Test]
    public async Task ValidateAsync_NoMatch_DoesNothing()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, Description = "clean" };
        ctx.Orders.Add(order);
        ctx.StopWord.Add(new StopWord { Id = 2, Word = "spam", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx);
        await svc.ValidateAsync(1);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(1));
    }

    [Test]
    public async Task ValidateAsync_IgnoresCase()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, Description = "bad WORD" };
        ctx.Orders.Add(order);
        ctx.StopWord.Add(new StopWord { Id = 5, Word = "word", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx);
        await svc.ValidateAsync(1);

        Assert.That(ctx.Set<BaseOrderStopWord>().Single().StopWordId, Is.EqualTo(5));
    }
}
