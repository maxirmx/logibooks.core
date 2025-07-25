// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

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
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class CompaniesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<CompaniesController>> _mockLogger;
    private IUserInformationService _userService;
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
        _userService = new UserInformationService(_dbContext);
        _controller = new CompaniesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _mockLogger.Object);
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
        _controller = new CompaniesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _mockLogger.Object);
    }

    [Test]
    public async Task CrudOperations_Work_ForAdmin()
    {
        SetCurrentUserId(1); // Admin user

        // Create three companies
        var dto1 = new CompanyDto { Inn = "111", Kpp = "111", Name = "Ozon", ShortName = "Ozon", CountryIsoNumeric = 840, PostalCode = "p1", City = "c1", Street = "s1" };
        var dto2 = new CompanyDto { Inn = "222", Kpp = "222", Name = "WBR", ShortName = "WBR", CountryIsoNumeric = 840, PostalCode = "p2", City = "c2", Street = "s2" };
        var dto3 = new CompanyDto { Inn = "333", Kpp = "333", Name = "Temp", ShortName = "Temp", CountryIsoNumeric = 840, PostalCode = "p3", City = "c3", Street = "s3" };

        // Add first company
        var created1 = await _controller.PostCompany(dto1);
        var createdDto1 = (created1.Result as CreatedAtActionResult)!.Value as CompanyDto;
        Assert.That(createdDto1!.Id, Is.EqualTo(1));

        // Add second company
        var created2 = await _controller.PostCompany(dto2);
        var createdDto2 = (created2.Result as CreatedAtActionResult)!.Value as CompanyDto;
        Assert.That(createdDto2!.Id, Is.EqualTo(2));

        // Add third company
        var created3 = await _controller.PostCompany(dto3);
        var createdDto3 = (created3.Result as CreatedAtActionResult)!.Value as CompanyDto;
        Assert.That(createdDto3!.Id, Is.EqualTo(3));

        // Test GET for company with ID 1
        var get = await _controller.GetCompany(1);
        Assert.That(get.Value!.Name, Is.EqualTo("Ozon"));

        // Test PUT for company with ID 1
        dto1.Id = 1;
        dto1.Name = "Ozon Updated";
        var update = await _controller.PutCompany(1, dto1);
        Assert.That(update, Is.TypeOf<NoContentResult>());

        // Verify the update was successful
        var updated = await _controller.GetCompany(1);
        Assert.That(updated.Value!.Name, Is.EqualTo("Ozon Updated"));

        // Verify all companies are in the list
        var list = await _controller.GetCompanies();
        Assert.That(list.Value!.Count(), Is.EqualTo(3));
        Assert.That(list.Value!.Any(c => c.Id == 1));
        Assert.That(list.Value!.Any(c => c.Id == 2));
        Assert.That(list.Value!.Any(c => c.Id == 3));

        // Test DELETE for company with ID 1 (should fail)
        var del1 = await _controller.DeleteCompany(1);
        Assert.That(del1, Is.TypeOf<ObjectResult>());
        var objResult1 = del1 as ObjectResult;
        Assert.That(objResult1!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(await _dbContext.Companies.FindAsync(1), Is.Not.Null);

        // Test DELETE for company with ID 2 (should fail)
        var del2 = await _controller.DeleteCompany(2);
        Assert.That(del2, Is.TypeOf<ObjectResult>());
        var objResult2 = del2 as ObjectResult;
        Assert.That(objResult2!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(await _dbContext.Companies.FindAsync(2), Is.Not.Null);

        // Test DELETE for company with ID 3 (should succeed)
        var del3 = await _controller.DeleteCompany(3);
        Assert.That(del3, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Companies.FindAsync(3), Is.Null);
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
    public async Task DeleteCompany_ReturnsConflict_WhenRegistersReferenceCompany_AndUserIsAdmin()
    {
        SetCurrentUserId(1); // Admin
        var company = new Company
        {
            Inn = "inn2",
            Kpp = "kpp2",
            Name = "TestCo2",
            ShortName = "TC2",
            CountryIsoNumeric = _country.IsoNumeric,
            PostalCode = "54321",
            City = "City2",
            Street = "Street2"
        };
        _dbContext.Companies.Add(company);
        await _dbContext.SaveChangesAsync();

        var register = new Register
        {
            CompanyId = company.Id,
            Company = company,
            FileName = "default_filename" 
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var res = await _controller.DeleteCompany(company.Id);

        Assert.That(res, Is.TypeOf<ObjectResult>());
        var obj = res as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        Assert.That(await _dbContext.Companies.FindAsync(company.Id), Is.Not.Null);
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
    public async Task PostCompany_Works_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new CompanyDto { Inn="i", Kpp="k", Name="n", ShortName="sn", CountryIsoNumeric=643, PostalCode="p", City="c", Street="s" };
        var created = await _controller.PostCompany(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
    }

    [Test]
    public async Task PostCompany_Fails_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new CompanyDto { Inn = "i", Kpp = "k", Name = "n", ShortName = "sn", CountryIsoNumeric = 643, PostalCode = "p", City = "c", Street = "s" };
        var res = await _controller.PostCompany(dto);
        Assert.That(res.Result, Is.TypeOf<ObjectResult>());
        var obj = res.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostCompany_ReturnsConflict_WhenInnAlreadyExists()
    {
        SetCurrentUserId(1);
        var existing = new Company { Inn="123", Kpp="1", Name="ex", ShortName="ex", CountryIsoNumeric=_country.IsoNumeric, PostalCode="p", City="c", Street="s" };
        _dbContext.Companies.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new CompanyDto { Inn="123", Kpp="2", Name="n", ShortName="sn", CountryIsoNumeric=_country.IsoNumeric, PostalCode="p2", City="c2", Street="s2" };
        var res = await _controller.PostCompany(dto);
        Assert.That(res.Result, Is.TypeOf<ObjectResult>());
        var obj = res.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task PutCompany_ReturnsConflict_WhenInnAlreadyExists()
    {
        SetCurrentUserId(1);
        var comp1 = new Company { Inn="inn1", Kpp="k1", Name="n1", ShortName="sn1", CountryIsoNumeric=_country.IsoNumeric, PostalCode="p1", City="c1", Street="s1" };
        var comp2 = new Company { Inn="inn2", Kpp="k2", Name="n2", ShortName="sn2", CountryIsoNumeric=_country.IsoNumeric, PostalCode="p2", City="c2", Street="s2" };
        _dbContext.Companies.AddRange(comp1, comp2);
        await _dbContext.SaveChangesAsync();

        var dto = new CompanyDto(comp2) { Inn = "inn1" };
        var res = await _controller.PutCompany(comp2.Id, dto);
        Assert.That(res, Is.TypeOf<ObjectResult>());
        var obj = res as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }
}
