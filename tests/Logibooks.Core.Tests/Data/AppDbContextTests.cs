using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;

namespace Logibooks.Core.Tests.Data;

public class AppDbContextTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("test_db")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public void CheckSameUser_ReturnsTrue_WhenIdsMatch()
    {
        using var ctx = CreateContext();
        Assert.True(ctx.CheckSameUser(1, 1));
    }

    [Test]
    public void CheckSameUser_ReturnsFalse_WhenIdsDiffer()
    {
        using var ctx = CreateContext();
        Assert.False(ctx.CheckSameUser(1, 2));
    }

    [Test]
    public void CheckSameUser_ReturnsFalse_WhenCuidZero()
    {
        using var ctx = CreateContext();
        Assert.False(ctx.CheckSameUser(1, 0));
    }
}
