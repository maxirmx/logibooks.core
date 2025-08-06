using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Threading.Tasks;
using System.Linq;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class ParcelViewsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<ParcelViewsController> _logger;
    private ParcelViewsController _controller;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"parcel_views_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<ParcelViewsController>();
        _controller = new ParcelViewsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new ParcelViewsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task Add_CreatesRecordForCurrentUser()
    {
        SetCurrentUserId(5);
        var result = await _controller.Add(new Reference { Id = 42 });
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var pv = _dbContext.ParcelViews.Single();
        Assert.That(pv.UserId, Is.EqualTo(5));
        Assert.That(pv.BaseOrderId, Is.EqualTo(42));
        Assert.That(pv.DTime, Is.Not.EqualTo(default(System.DateTime)));
    }

    [Test]
    public async Task Back_ReturnsLastAddedAndRemovesIt()
    {
        SetCurrentUserId(7);
        _dbContext.ParcelViews.AddRange(
            new ParcelView { UserId = 7, BaseOrderId = 1, DTime = System.DateTime.UtcNow.AddMinutes(-5) },
            new ParcelView { UserId = 7, BaseOrderId = 2, DTime = System.DateTime.UtcNow },
            new ParcelView { UserId = 8, BaseOrderId = 3, DTime = System.DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Back();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(2));
        Assert.That(_dbContext.ParcelViews.Count(p => p.UserId == 7), Is.EqualTo(1));
        Assert.That(_dbContext.ParcelViews.Any(p => p.UserId == 7 && p.BaseOrderId == 2), Is.False);
    }

    [Test]
    public async Task Back_ReturnsNoContentWhenNoRecords()
    {
        SetCurrentUserId(1);
        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }
}
