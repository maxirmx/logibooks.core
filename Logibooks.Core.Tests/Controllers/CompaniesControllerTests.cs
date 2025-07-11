using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class CompaniesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<CompaniesController>> _mockLogger;
    private CompaniesController _controller;
    private Role _adminRole;
    private Role _userRole;
    private User _adminUser;
    private User _regularUser;
    private Country _country;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"companies_controller_db_{System.Guid.NewGuid()}")
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
        _country = new Country { IsoNumeric = 840, IsoAlpha2 = "US" };
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        _dbContext.Countries.Add(_country);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<CompaniesController>>();
        _controller = new CompaniesController(_mockHttpContextAccessor.Object, _dbContext, _mockLogger.Object);
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
        _controller = new CompaniesController(_mockHttpContextAccessor.Object, _dbContext, _mockLogger.Object);
    }

    [Test]
    public async Task CrudOperations_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new CompanyDto { Inn = "1", Kpp = "2", Name = "N", ShortName = "SN", CountryIsoNumeric = 840, PostalCode = "p", City = "c", Street = "s" };
        var created = await _controller.PostCompany(dto);
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as CompanyDto;
        int id = createdDto!.Id;

        var get = await _controller.GetCompany(id);
        Assert.That(get.Value!.Name, Is.EqualTo("N"));

        dto.Id = id;
        dto.Name = "N2";
        var update = await _controller.PutCompany(id, dto);
        Assert.That(update, Is.TypeOf<NoContentResult>());

        var updated = await _controller.GetCompany(id);
        Assert.That(updated.Value!.Name, Is.EqualTo("N2"));

        var list = await _controller.GetCompanies();
        Assert.That(list.Value!.Any(c => c.Id == id));

        var del = await _controller.DeleteCompany(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Companies.FindAsync(id), Is.Null);
    }

    [Test]
    public async Task PutCompany_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var comp = new Company { Inn="i", Kpp="k", Name="n", ShortName="sn", CountryIsoNumeric=840, PostalCode="p", City="c", Street="s" };
        _dbContext.Companies.Add(comp);
        await _dbContext.SaveChangesAsync();

        var dto = new CompanyDto(comp) { Name = "upd" };
        var res = await _controller.PutCompany(comp.Id, dto);
        Assert.That(res, Is.TypeOf<ObjectResult>());
        var obj = res as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteCompany_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var comp = new Company { Inn="i", Kpp="k", Name="n", ShortName="sn", CountryIsoNumeric=840, PostalCode="p", City="c", Street="s" };
        _dbContext.Companies.Add(comp);
        await _dbContext.SaveChangesAsync();

        var res = await _controller.DeleteCompany(comp.Id);
        Assert.That(res, Is.TypeOf<ObjectResult>());
        var obj = res as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetCompany_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var res = await _controller.GetCompany(999);
        Assert.That(res.Result, Is.TypeOf<ObjectResult>());
        var obj = res.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task PostCompany_Works_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new CompanyDto { Inn="i", Kpp="k", Name="n", ShortName="sn", CountryIsoNumeric=840, PostalCode="p", City="c", Street="s" };
        var created = await _controller.PostCompany(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
    }
}
