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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class CustomsProceduresControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<CustomsProceduresController> _logger;
    private CustomsProceduresController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"customs_proc_controller_db_{System.Guid.NewGuid()}")
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
        _logger = new LoggerFactory().CreateLogger<CustomsProceduresController>();
        _controller = new CustomsProceduresController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new CustomsProceduresController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetProcedures_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.CustomsProcedures.AddRange(
            new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" },
            new CustomsProcedure { Id = 2, Code = 60, Name = "Реимпорт" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetProcedures();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetProcedure_ReturnsRecord_WhenExists()
    {
        SetCurrentUserId(2);
        _dbContext.CustomsProcedures.Add(new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetProcedure(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value!.Code, Is.EqualTo(10));
        Assert.That(result.Value!.Name, Is.EqualTo("Экспорт"));
    }

    [Test]
    public async Task GetProcedure_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(2);

        var result = await _controller.GetProcedure(42);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
