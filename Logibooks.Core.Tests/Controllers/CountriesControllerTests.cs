using System.Linq;
using System.Collections.Generic;
using System.Threading;
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
using Logibooks.Core.Services;
using System.Net.Http;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class CountriesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<CountriesController>> _mockLogger;
    private Mock<IUpdateCountriesService> _mockService;
    private IUserInformationService _userService;
    private CountriesController _controller;
    private Role _adminRole;
    private Role _userRole;
    private User _adminUser;
    private User _regularUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_controller_db_{System.Guid.NewGuid()}")
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
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _adminRole } ]
        };
        _regularUser = new User
        {
            Id = 2,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 2, RoleId = 2, Role = _userRole } ]
        };
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<CountriesController>>();
        _mockService = new Mock<IUpdateCountriesService>();
        _userService = new UserInformationService(_dbContext);
        _controller = new CountriesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _mockService.Object, _mockLogger.Object);
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
        _controller = new CountriesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _mockService.Object, _mockLogger.Object);
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
    public async Task Update_RunsService_ForAdmin()
    {
        SetCurrentUserId(1);
        var result = await _controller.Update();
        _mockService.Verify(s => s.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task GetCodes_ReturnsList_ForAnyUser()
    {
        SetCurrentUserId(2);
        _dbContext.Countries.AddRange(new Country { IsoNumeric = 840, IsoAlpha2 = "US" },
                                         new Country { IsoNumeric = 124, IsoAlpha2 = "CA" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetCodes();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetCode_ReturnsRecord_ForAnyUser()
    {
        SetCurrentUserId(2);
        _dbContext.Countries.Add(new Country { IsoNumeric = 840, IsoAlpha2 = "US" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetCode(840);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.IsoNumeric, Is.EqualTo(840));
    }

    [Test]
    public async Task GetCodesCompact_OrdersAndSelectsProperly()
    {
        SetCurrentUserId(2);
        _dbContext.Countries.AddRange(
            new Country { IsoNumeric = 124, IsoAlpha2 = "CA", NameEnOfficial = "CA", NameRuOfficial = "CA" },
            new Country { IsoNumeric = 792, IsoAlpha2 = "TR", NameEnOfficial = "TR", NameRuOfficial = "TR" },
            new Country { IsoNumeric = 643, IsoAlpha2 = "RU", NameEnOfficial = "RU", NameRuOfficial = "RU" },
            new Country { IsoNumeric = 860, IsoAlpha2 = "UZ", NameEnOfficial = "UZ", NameRuOfficial = "UZ" },
            new Country { IsoNumeric = 31,  IsoAlpha2 = "AZ", NameEnOfficial = "AZ", NameRuOfficial = "AZ" },
            new Country { IsoNumeric = 398, IsoAlpha2 = "KZ", NameEnOfficial = "KZ", NameRuOfficial = "KZ" },
            new Country { IsoNumeric = 268, IsoAlpha2 = "GE", NameEnOfficial = "GE", NameRuOfficial = "GE" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetCodesCompact();
        var list = result.Value!.ToList();

        string[] expectedFirst = ["RU", "UZ", "GE", "AZ", "TR"];
        Assert.That(list.Take(5).Select(c => c.IsoAlpha2), Is.EqualTo(expectedFirst));
        var rest = list.Skip(5).Select(c => c.IsoNumeric).ToList();
        Assert.That(rest, Is.EqualTo(rest.OrderBy(n => n).ToList()));
        Assert.That(list.All(c => c.NameEnOfficial != string.Empty && c.NameRuOfficial != string.Empty));
    }
}
