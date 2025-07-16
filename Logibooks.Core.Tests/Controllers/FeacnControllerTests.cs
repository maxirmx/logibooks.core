using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<FeacnController> _logger;
    private FeacnController _controller;
    private Role _adminRole;
    private Role _userRole;
    private User _adminUser;
    private User _regularUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacn_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Admin" };
        _userRole = new Role { Id = 2, Name = "user", Title = "User" };
        _dbContext.Roles.AddRange(_adminRole, _userRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }]
        };
        _regularUser = new User
        {
            Id = 2,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _userRole }]
        };
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<FeacnController>();
        _controller = new FeacnController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new FeacnController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task Update_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var result = await _controller.Update();
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Update_ReturnsNoContent_ForAdmin()
    {
        SetCurrentUserId(1);
        var result = await _controller.Update();
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task GetAll_ReturnsData_ForAnyUser()
    {
        SetCurrentUserId(2);
        var order = new FEACNOrder { Id = 1, Number = 1 };
        var prefix = new FEACNPrefix { Id = 2, Code = "12", FeacnOrderId = 1, FeacnOrder = order };
        var ex = new FEACNPrefixException { Id = 3, Code = "12a", FeacnPrefixId = 2, FeacnPrefix = prefix };
        _dbContext.FEACNOrders.Add(order);
        _dbContext.FEACNPrefixes.Add(prefix);
        _dbContext.FEACNPrefixExceptions.Add(ex);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetAll();

        Assert.That(result.Value, Is.Not.Null);
        var dto = result.Value!;
        Assert.That(dto.Orders.Count, Is.EqualTo(1));
        Assert.That(dto.Prefixes.Count, Is.EqualTo(1));
        Assert.That(dto.Exceptions.Count, Is.EqualTo(1));
    }
}
