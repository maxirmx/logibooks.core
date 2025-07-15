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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        _dbContext.Statuses.AddRange(new OrderStatus { Id = 1,  Title = "Loaded" },
                                     new OrderStatus { Id = 2,  Title = "Processed" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStatuses();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new OrderStatusDto { Title = "New" };
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
        var dto = new OrderStatusDto { Title = "t" };
        var result = await _controller.CreateStatus(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteStatus_ReturnsConflict_WhenUsed()
    {
        SetCurrentUserId(1);
        var status = new OrderStatus { Id = 5, Title = "Used" };
        var reg = new Register { Id = 1, FileName = "r" };
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 5 };
        _dbContext.Statuses.Add(status);
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteStatus(5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task GetStatus_ReturnsStatus_WhenLogistAndStatusExists()
    {
        // Arrange
        SetCurrentUserId(2); // Logist
        var status = new OrderStatus { Id = 3, Title = "Specific Status" };
        _dbContext.Statuses.Add(status);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(3);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Result, Is.Null);
        var statusDto = result.Value;
        Assert.That(statusDto!.Id, Is.EqualTo(3));
        Assert.That(statusDto.Title, Is.EqualTo("Specific Status"));
    }

    [Test]
    public async Task GetStatus_ReturnsStatus_WhenAdminAndStatusExists()
    {
        // Arrange
        SetCurrentUserId(1); // Admin
        var status = new OrderStatus { Id = 4, Title = "Admin Viewable Status" };
        _dbContext.Statuses.Add(status);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(4);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Result, Is.Null);
        var statusDto = result.Value;
        Assert.That(statusDto!.Id, Is.EqualTo(4));
        Assert.That(statusDto.Title, Is.EqualTo("Admin Viewable Status"));
    }

    [Test]
    public async Task GetStatus_ReturnsNotFound_WhenStatusDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(2); // Logist

        // Act
        var result = await _controller.GetStatus(999); // Non-existent ID

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(result.Value, Is.Null);

        var errMsg = obj.Value as ErrMessage;
        Assert.That(errMsg, Is.Not.Null);
        StringAssert.Contains("999", errMsg!.Msg);
    }

    [Test]
    public async Task GetStatus_VerifiesPropertiesOnReturnedDto()
    {
        // Arrange
        SetCurrentUserId(2); // Logist
        var status = new OrderStatus { Id = 6, Title = "Property Test Status" };
        _dbContext.Statuses.Add(status);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetStatus(6);

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var statusDto = result.Value;

        // Verify properties were mapped correctly
        Assert.That(statusDto!.Id, Is.EqualTo(6));
        Assert.That(statusDto.Title, Is.EqualTo("Property Test Status"));

    }
}
