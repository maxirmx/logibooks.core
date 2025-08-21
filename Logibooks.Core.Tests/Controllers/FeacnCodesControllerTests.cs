using System;
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

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnCodesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<FeacnCodesController> _logger;
    private FeacnCodesController _controller;
    private Role _userRole;
    private User _user;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacncodes_controller_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _userRole = new Role { Id = 1, Name = "user", Title = "User" };
        _dbContext.Roles.Add(_userRole);
        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _user = new User
        {
            Id = 1,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _userRole } ]
        };
        _dbContext.Users.Add(_user);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<FeacnCodesController>();
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task Get_ReturnsDto_WhenExists()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode { Id = 10, Code = "1234567890", CodeEx = "1234567890", Name = "Name", NormalizedName = "NAME" };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(10));
    }

    [Test]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.Get(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetByCode_ReturnsBadRequest_OnInvalidCode()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetByCode("123");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetByCode_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetByCode("1234567890");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetByCode_ReturnsDto_WhenExists()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode { Id = 20, Code = "1234567890", CodeEx = "1234567890", Name = "N1", NormalizedName = "N1" };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567890");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1234567890"));
    }

    [Test]
    public async Task Lookup_ReturnsMatchingCodes()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "A", NormalizedName = "ABC", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "B", NormalizedName = "XYZ", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "C", NormalizedName = "ABC", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Lookup("abc");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Code, Is.EqualTo("1111111111"));
    }

    [Test]
    public async Task Children_ReturnsTopLevel_WhenIdNull()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "root", NormalizedName = "ROOT" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "child", NormalizedName = "CHILD", ParentId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(null);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Children_ReturnsChildren_ForGivenId()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "root", NormalizedName = "ROOT" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "child1", NormalizedName = "CHILD1", ParentId = 1 },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "child2", NormalizedName = "CHILD2", ParentId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }
}
