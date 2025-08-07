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
        // Arrange
        SetCurrentUserId(5);
        var order = new WbrOrder { Id = 42, RegisterId = 1, StatusId = 1 };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        
        // Act
        var result = await _controller.Add(new Reference { Id = 42 });
        
        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var pv = _dbContext.ParcelViews.Single();
        Assert.That(pv.UserId, Is.EqualTo(5));
        Assert.That(pv.BaseOrderId, Is.EqualTo(42));
        Assert.That(pv.DTime, Is.Not.EqualTo(default(System.DateTime)));
    }

    [Test]
    public async Task Add_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(5);
        
        // Act
        var result = await _controller.Add(new Reference { Id = 999 });
        
        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var notFoundResult = result as ObjectResult;
        var errorMessage = notFoundResult!.Value as ErrMessage;
        Assert.That(errorMessage!.Msg, Does.Contain("999"));
        Assert.That(_dbContext.ParcelViews.Any(), Is.False, "No ParcelView should be created");
    }

    [Test]
    public async Task Back_RemovesLastTwiceAndReturnsOrderViewItemWithDTime()
    {
        SetCurrentUserId(7);
        var order1 = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 };
        var order2 = new WbrOrder { Id = 2, RegisterId = 1, StatusId = 1 };
        var order3 = new WbrOrder { Id = 3, RegisterId = 1, StatusId = 1 };
        _dbContext.Orders.AddRange(order1, order2, order3);
        _dbContext.ParcelViews.AddRange(
            new ParcelView { UserId = 7, BaseOrderId = 1, DTime = System.DateTime.UtcNow.AddMinutes(-10) },
            new ParcelView { UserId = 7, BaseOrderId = 2, DTime = System.DateTime.UtcNow.AddMinutes(-5) },
            new ParcelView { UserId = 7, BaseOrderId = 3, DTime = System.DateTime.UtcNow },
            new ParcelView { UserId = 8, BaseOrderId = 4, DTime = System.DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var item = okResult!.Value as OrderViewItem;
        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Id, Is.EqualTo(2));
        Assert.That(item.DTime, Is.Not.Null);

        Assert.That(_dbContext.ParcelViews.Count(p => p.UserId == 7), Is.EqualTo(1));
        Assert.That(_dbContext.ParcelViews.Any(p => p.UserId == 7 && p.BaseOrderId == 2), Is.False);
        Assert.That(_dbContext.ParcelViews.Any(p => p.UserId == 7 && p.BaseOrderId == 3), Is.False);
        Assert.That(_dbContext.ParcelViews.Any(p => p.UserId == 7 && p.BaseOrderId == 1), Is.True);
    }

    [Test]
    public async Task Back_ReturnsNoContentWhenNoRecords()
    {
        SetCurrentUserId(1);
        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Back_ReturnsNoContent_WhenNoParcelViewsExist()
    {
        SetCurrentUserId(10);
        // No ParcelViews for user 10
        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Back_ReturnsNoContent_WhenOnlyOneParcelViewExists()
    {
        SetCurrentUserId(11);
        var order = new WbrOrder { Id = 100, RegisterId = 1, StatusId = 1 };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        _dbContext.ParcelViews.Add(new ParcelView { UserId = 11, BaseOrderId = 100, DTime = System.DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Back_ReturnsNoContent_WhenSecondParcelViewHasNoBaseOrder()
    {
        SetCurrentUserId(12);
        var order = new WbrOrder { Id = 200, RegisterId = 1, StatusId = 1 };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        _dbContext.ParcelViews.AddRange(
            new ParcelView { UserId = 12, BaseOrderId = 200, DTime = System.DateTime.UtcNow.AddMinutes(-5) },
            new ParcelView { UserId = 12, BaseOrderId = 201, DTime = System.DateTime.UtcNow }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.Back();
        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }
}
