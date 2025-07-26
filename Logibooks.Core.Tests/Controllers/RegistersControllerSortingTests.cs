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

using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;
using Moq;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class RegistersControllerSortingTests
{
#pragma warning disable CS8618
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
#pragma warning restore CS8618
    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"register_sorting_db_{System.Guid.NewGuid()}")
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
        _logger = new LoggerFactory().CreateLogger<RegistersController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object);
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object);
    }

    // Move all sorting-related tests here and add new ones for extended sorting
    // Sorting by FileName descending
    [Test]
    public async Task GetRegisters_SortsByFileName_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "a.xlsx", CompanyId = 2 },
            new Register { Id = 2, FileName = "b.xlsx", CompanyId = 2 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "fileName", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].FileName, Is.EqualTo("b.xlsx"));
        Assert.That(items[1].FileName, Is.EqualTo("a.xlsx"));
    }

    // Sorting by OrdersTotal across pages
    [Test]
    public async Task GetRegisters_SortsByOrdersTotalAcrossPages()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx" , CompanyId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx" , CompanyId = 2 },
            new Register { Id = 3, FileName = "r3.xlsx" , CompanyId = 2 },
            new Register { Id = 4, FileName = "r4.xlsx" , CompanyId = 2 }
        );
        _dbContext.Orders.AddRange(
            new WbrOrder { RegisterId = 1, StatusId = 1 },
            new WbrOrder { RegisterId = 2, StatusId = 1 },
            new WbrOrder { RegisterId = 2, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var r1 = await _controller.GetRegisters(page: 1, pageSize: 2, sortBy: "ordersTotal", sortOrder: "desc");
        var ok1 = r1.Result as OkObjectResult;
        var pr1 = ok1!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr1!.Items.First().Id, Is.EqualTo(3));
        var r2 = await _controller.GetRegisters(page: 2, pageSize: 2, sortBy: "ordersTotal", sortOrder: "desc");
        var ok2 = r2.Result as OkObjectResult;
        var pr2 = ok2!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr2!.Items.First().Id, Is.EqualTo(1));
    }

    // Sorting by CompanyShortName descending
    [Test]
    public async Task GetRegisters_SortsByCompanyShortName_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "companyId", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].CompanyId, Is.EqualTo(2));  // "ООО \"РВБ\"" comes before "ООО \"Интернет Решения\""
        Assert.That(items[1].CompanyId, Is.EqualTo(1));
    }

    // Sorting by CountryAlpha2 descending
    [Test]
    public async Task GetRegisters_SortsByCountryAlpha2_Ascending()
    {
        SetCurrentUserId(1);
        // Inside the Setup() method, after adding the Russian country
        _dbContext.Countries.Add(new Country
        {
            IsoNumeric = 860,
            IsoAlpha2 = "UZ",
            NameEnShort = "Uzbekistan",
            NameEnFormal = "Republic of Uzbekistan",
            NameEnOfficial = "Republic of Uzbekistan",
            NameEnCldr = "Uzbekistan",
            NameRuShort = "Узбекистан",
            NameRuFormal = "Республика Узбекистан",
            NameRuOfficial = "Республика Узбекистан"
        });
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, DestCountryCode = 860 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, DestCountryCode = 643 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "countryAlpha2", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].DestCountryCode, Is.EqualTo(860)); // "RU" comes after "UZ"
        Assert.That(items[1].DestCountryCode, Is.EqualTo(643));
    }

    // Sorting by TransportationTypeName ascending
    [Test]
    public async Task GetRegisters_SortsByTransportationTypeName_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, TransportationTypeId = 2 },  
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TransportationTypeId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "transportationTypeId", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].TransportationTypeId, Is.EqualTo(1)); // "Авиа" comes before "Авто"
        Assert.That(items[1].TransportationTypeId, Is.EqualTo(2));
    }

    // Sorting by CustomsProcedureName ascending
    [Test]
    public async Task GetRegisters_SortsByCustomsProcedureName_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, CustomsProcedureId = 1 },   // Экспорт
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, CustomsProcedureId = 2 }    // Реимпорт 
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "customsProcedureId", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].CustomsProcedureId, Is.EqualTo(2)); // "Экспорт" comes after "Реимпорт"
        Assert.That(items[1].CustomsProcedureId, Is.EqualTo(1));
    }

    // Sorting by InvoiceNumber ascending
    [Test]
    public async Task GetRegisters_SortsByInvoiceNumber_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, InvoiceNumber = "B" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, InvoiceNumber = "A" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "invoiceNumber", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].InvoiceNumber, Is.EqualTo("A"));
        Assert.That(items[1].InvoiceNumber, Is.EqualTo("B"));
    }

    // Sorting by InvoiceDate ascending
    [Test]
    public async Task GetRegisters_SortsByInvoiceDate_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, InvoiceDate = new DateOnly(2025, 1, 2) },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, InvoiceDate = new DateOnly(2025, 1, 1) }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "invoiceDate", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].InvoiceDate, Is.EqualTo(new DateOnly(2025, 1, 1)));
        Assert.That(items[1].InvoiceDate, Is.EqualTo(new DateOnly(2025, 1, 2)));
    }
}
