using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class OrderStatusesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<OrderStatusesController> _logger;
    private OrderStatusesController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"order_statuses_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Администратор" };
        _logistRole = new Role { Id = 2, Name = "logist", Title = "Логист" };
        _dbContext.Roles.AddRange(_adminRole, _logistRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }]
        };
        _logistUser = new User
        {
            Id = 2,
            Email = "logist@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _logistRole }]
        };
        _dbContext.Users.AddRange(_adminUser, _logistUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<OrderStatusesController>();
        _controller = new OrderStatusesController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new OrderStatusesController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetStatuses_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.Statuses.AddRange(new OrderStatus { Id = 1, Name = "loaded", Title = "Loaded" },
                                     new OrderStatus { Id = 2, Name = "processed", Title = "Processed" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStatuses();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetStatuses_ReturnsForbidden_ForUnknown()
    {
        SetCurrentUserId(99);
        var result = await _controller.GetStatuses();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new OrderStatusDto { Name = "new", Title = "New" };
        var created = await _controller.CreateStatus(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as OrderStatusDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Title = "Updated";
        var upd = await _controller.UpdateStatus(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteStatus(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new OrderStatusDto { Name = "n", Title = "t" };
        var result = await _controller.CreateStatus(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteStatus_ReturnsConflict_WhenUsed()
    {
        SetCurrentUserId(1);
        var status = new OrderStatus { Id = 5, Name = "used", Title = "Used" };
        var reg = new Register { Id = 1, FileName = "r" };
        var order = new Order { Id = 1, RegisterId = 1, StatusId = 5 };
        _dbContext.Statuses.Add(status);
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteStatus(5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }
}
