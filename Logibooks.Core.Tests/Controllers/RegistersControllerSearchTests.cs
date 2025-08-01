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
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
// OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using System.Threading.Tasks;
using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using System.IO;
using System.Threading;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class RegistersControllerSearchTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IRegisterValidationService> _mockRegValidationService;
    private ILogger<RegistersController> _logger;
    private IUserInformationService _userService;
    private Role _logistRole;
    private Role _adminRole;
    private User _logistUser;
    private User _adminUser;
    private RegistersController _controller;
    private Mock<IRegisterProcessingService> _mockProcessingService;
    private Mock<IOrderIndPostGenerator> _mockIndPostGenerator;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"register_search_db_{System.Guid.NewGuid()}")
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
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _logistRole }]
        };
        _adminUser = new User
        {
            Id = 2,
            Email = "admin@example.com",
            Password = hpw,
            FirstName = "Adm",
            LastName = "User",
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _adminRole }]
        };
        _dbContext.Users.AddRange(_logistUser, _adminUser);
        _dbContext.Countries.Add(new Country {
            IsoNumeric = 643,
            IsoAlpha2 = "RU",
            NameRuShort = "Российская Федерация"
        });
        _dbContext.Countries.Add(new Country
        {
            IsoNumeric = 860,
            IsoAlpha2 = "UZ",
            NameRuShort = "Узбекистан"
        });
        _dbContext.Companies.AddRange(
            new Company {
                Id = 1,
                Inn = "7704217370",
                Kpp = "997750001",
                Name = "ООО \"Интернет Решения\"",
                ShortName = "",
                CountryIsoNumeric = 643,
                PostalCode = "123112",
                City = "Москва",
                Street = "Пресненская набережная д.10, пом.1, этаж 41, ком.6"
            },
            new Company {
                Id = 2,
                Inn = "9714053621",
                Kpp = "507401001",
                Name = "",
                ShortName = "ООО \"РВБ\"",
                CountryIsoNumeric = 643,
                PostalCode = "",
                City = "д. Коледино",
                Street = "Индустриальный Парк Коледино, д.6, стр.1"
            },
            new Company {
                Id = 3,
                Inn = "200892688",
                Kpp = "",
                Name = "АО \"Узбекпочта\"",
                ShortName = "Узбекпочта",
                CountryIsoNumeric = 860,
                PostalCode = "100047",
                City = "Ташкент",
                Street = "ул. Навои, 28"
            }
        );
        _dbContext.TransportationTypes.AddRange(
            new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" },
            new TransportationType { Id = 2, Code = TransportationTypeCode.Auto, Name = "Авто" }
        );
        _dbContext.CustomsProcedures.AddRange(
            new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" },
            new CustomsProcedure { Id = 2, Code = 60, Name = "Реимпорт" }
        );
        _dbContext.SaveChanges();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockRegValidationService = new Mock<IRegisterValidationService>();
        _mockProcessingService = new Mock<IRegisterProcessingService>();
        _mockIndPostGenerator = new Mock<IOrderIndPostGenerator>();
        _logger = new LoggerFactory().CreateLogger<RegistersController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object, _mockIndPostGenerator.Object);
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object, _mockIndPostGenerator.Object);
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByFileName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "report1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "other.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "report");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("report1.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByInvoiceNumber()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-123" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-456" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "123");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().InvoiceNumber, Is.EqualTo("INV-123"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByDealNumber()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-2024-001" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-2024-002" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "001");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().DealNumber, Is.EqualTo("DEAL-2024-001"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCompanyShortName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "РВБ");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().CompanyId, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByTheOtherCompanyShortName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1, TheOtherCompanyId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Узбекпочта");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TheOtherCompanyId, Is.EqualTo(3));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCountryName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TheOtherCountryCode = 643 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TheOtherCountryCode = 860 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Российская");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TheOtherCountryCode, Is.EqualTo(643));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByTransportationTypeName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, TheOtherCompanyId = 3, TransportationTypeId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TransportationTypeId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Авиа");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TransportationTypeId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCustomsProcedureName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, TheOtherCompanyId = 3, CustomsProcedureId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Экспорт");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().CustomsProcedureId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_SearchIsCaseInsensitive()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "report.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "other.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "REPORT");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("report.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchWithPartialMatches()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "quarterly_report.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "monthly_data.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "quarter");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("quarterly_report.xlsx"));
    }
}
