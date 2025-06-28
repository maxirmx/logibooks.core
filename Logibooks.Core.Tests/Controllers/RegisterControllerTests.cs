using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NUnit.Framework;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class RegisterControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<RegisterController> _logger;
    private RegisterController _controller;
    private Role _logistRole;
    private Role _adminRole;
    private User _logistUser;
    private User _adminUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"register_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _logistRole = new Role { Id = 1, Name = "logist", Title = "Логист" };
        _adminRole = new Role { Id = 2, Name = "administrator", Title = "Администратор" };
        _dbContext.Roles.AddRange(_logistRole, _adminRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _logistUser = new User
        {
            Id = 1,
            Email = "logist@example.com",
            Password = hpw,
            FirstName = "Log",
            LastName = "User",
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _logistRole } ]
        };
        _adminUser = new User
        {
            Id = 2,
            Email = "admin@example.com",
            Password = hpw,
            FirstName = "Adm",
            LastName = "User",
            UserRoles = [ new UserRole { UserId = 2, RoleId = 2, Role = _adminRole } ]
        };
        _dbContext.Users.AddRange(_logistUser, _adminUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<RegisterController>();
        _controller = new RegisterController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new RegisterController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetRegisters_ReturnsData_ForLogist()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.InstanceOf<IEnumerable<RegisterItem>>());
    }

    [Test]
    public async Task GetRegisters_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DownloadRegister_ReturnsXlsx()
    {
        SetCurrentUserId(1);
        var res = await _controller.DownloadRegister();
        Assert.That(res, Is.InstanceOf<FileContentResult>());
        var file = res as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("register.xlsx"));
    }

    [Test]
    public async Task DownloadRegister_ReturnsZipWhenRequested()
    {
        SetCurrentUserId(1);
        var res = await _controller.DownloadRegister("zip");
        Assert.That(res, Is.InstanceOf<FileContentResult>());
        var file = res as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("register.zip"));
        Assert.That(file.ContentType, Is.EqualTo("application/zip"));
    }
}
