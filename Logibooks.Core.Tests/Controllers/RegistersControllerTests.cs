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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Linq;
using System.Threading;
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
public class RegistersControllerTests
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
    private Mock<IOrderIndPostGenerator> _mockIndPostGenerator;
#pragma warning restore CS8618

    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");


    // Initialize the _controller field in the Setup method
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

        // Add real country and companies as in AppDbContext
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
        _mockIndPostGenerator = new Mock<IOrderIndPostGenerator>();
        _logger = new LoggerFactory().CreateLogger<RegistersController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object, _mockIndPostGenerator.Object);
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
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockRegValidationService.Object, _mockProcessingService.Object, _mockIndPostGenerator.Object);
    }

    [Test]
    public async Task GetRegisters_ReturnsData_ForLogist()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.InstanceOf<PagedResult<RegisterViewItem>>());
    }

    [Test]
    public async Task GetRegisters_ReturnsOrderCounts()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new OrderCheckStatus { Id = 1,  Title = "Loaded" },
            new OrderCheckStatus { Id = 2,  Title = "Processed" }
        );
        var r1 = new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2 };
        var r2 = new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2 };
        _dbContext.Registers.AddRange(r1, r2);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 2 },
            new WbrOrder { Id = 3, RegisterId = 2, StatusId = 2 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters();
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.OrderBy(i => i.Id).ToArray();

        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].OrdersTotal, Is.EqualTo(2));
        Assert.That(items[0].OrdersByStatus[1], Is.EqualTo(1));
        Assert.That(items[0].OrdersByStatus[2], Is.EqualTo(1));
        Assert.That(items[1].OrdersTotal, Is.EqualTo(1));
        Assert.That(items[1].OrdersByStatus[2], Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_ReturnsZeroOrders_WhenNoOrders()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters();
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.OrderBy(i => i.Id).ToArray();

        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].OrdersTotal, Is.EqualTo(0));
        Assert.That(items[0].OrdersByStatus.Count, Is.EqualTo(0));
        Assert.That(items[1].OrdersTotal, Is.EqualTo(0));
        Assert.That(items[1].OrdersByStatus.Count, Is.EqualTo(0));
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
    public async Task GetRegister_ReturnsRegister_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value.FileName, Is.EqualTo("reg.xlsx"));
        Assert.That(result.Value.OrdersTotal, Is.EqualTo(0));
        Assert.That(result.Value.OrdersByStatus.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetRegister_ReturnsOrderCounts()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new OrderCheckStatus { Id = 1,  Title = "Loaded" },
            new OrderCheckStatus { Id = 2,  Title = "Processed" }
        );
        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 2 },
            new WbrOrder { Id = 3, RegisterId = 1, StatusId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.OrdersTotal, Is.EqualTo(3));
        Assert.That(result.Value.OrdersByStatus[1], Is.EqualTo(2));
        Assert.That(result.Value.OrdersByStatus[2], Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegister_ReturnsOrderCounts_WithMultipleStatusGroups()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new OrderCheckStatus { Id = 1,  Title = "Loaded" },
            new OrderCheckStatus { Id = 2,  Title = "Processed" },
            new OrderCheckStatus { Id = 3,  Title = "Delivered" }
        );
        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 2 },
            new WbrOrder { Id = 3, RegisterId = 1, StatusId = 3 },
            new WbrOrder { Id = 4, RegisterId = 1, StatusId = 3 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.OrdersTotal, Is.EqualTo(4));
        Assert.That(result.Value.OrdersByStatus[1], Is.EqualTo(1));
        Assert.That(result.Value.OrdersByStatus[2], Is.EqualTo(1));
        Assert.That(result.Value.OrdersByStatus[3], Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        _dbContext.Registers.Add(new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2 });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetRegister_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.GetRegister(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenPageIsZero()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(page: 0);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenPageIsNegative()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(page: -1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenPageSizeIsZero()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(pageSize: 0);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenPageSizeIsNegative()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(pageSize: -5);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenPageSizeExceedsMaximum()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(pageSize: 101);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenSortByIsInvalid()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(sortBy: "invalidfield");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenSortOrderIsInvalid()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(sortOrder: "invalid");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetRegisters_AcceptsValidSortByValues()
    {
        SetCurrentUserId(1);
        string[] validSortBy = ["id", "filename", "date", "orderstotal"];

        foreach (var sortBy in validSortBy)
        {
            var result = await _controller.GetRegisters(sortBy: sortBy);
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>(),
                $"sortBy '{sortBy}' should be valid");
        }
    }

    [Test]
    public async Task GetRegisters_AcceptsValidSortOrderValues()
    {
        SetCurrentUserId(1);
        string[] validSortOrders = ["asc", "desc"];

        foreach (var sortOrder in validSortOrders)
        {
            var result = await _controller.GetRegisters(sortOrder: sortOrder);
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>(),
                $"sortOrder '{sortOrder}' should be valid");
        }
    }

    [Test]
    public async Task GetRegisters_AcceptsMaxPageSize()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(pageSize: 100);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task GetRegisters_ReturnsAll_WhenPageSizeIsMinusOne()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1 },
            new Register { Id = 3, FileName = "r3.xlsx", CompanyId = 2 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(pageSize: -1);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Items.Count(), Is.EqualTo(3));
        Assert.That(pr.Pagination.TotalCount, Is.EqualTo(3));
        Assert.That(pr.Pagination.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_PageExceedsTotalPages_ResetsToFirstPage()
    {
        SetCurrentUserId(1);
        for (int i = 1; i <= 6; i++)
        {
            _dbContext.Registers.Add(new Register { Id = i, FileName = $"r{i}.xlsx", CompanyId = 2 });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(page: 3, pageSize: 5);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.CurrentPage, Is.EqualTo(1));
        Assert.That(pr.Items.First().Id, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenNoFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenEmptyFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenUnsupportedFileType()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = CreateMockFile("test.pdf", "application/pdf", new byte[] { 0x01 });

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файлы формата .pdf не поддерживаются. Можно загрузить .xlsx, .xls, .zip, .rar"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenExcelFileUploaded()
    {
        SetCurrentUserId(1); // Logist user  

        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.xlsx");
        byte[] excelContent;

        try
        {
            excelContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to return a successful reference
        var expectedReference = new Reference { Id = 123 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("Реестр_207730349.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Verify the CreatedAtActionResult properties
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult!.Value, Is.TypeOf<Reference>());
        var returnedReference = createdResult.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenZipWithoutExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory without any Excel files
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.txt");
            using var entryStream = entry.Open();
            byte[] textContent = System.Text.Encoding.UTF8.GetBytes("Test content");
            entryStream.Write(textContent, 0, textContent.Length);
        }

        var mockFile = CreateMockFile("test.zip", "application/zip", zipStream.ToArray());

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файл реестра не найден в архиве"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenZipWithExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Load test zip file from test.data folder
        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.zip");

        byte[] zipContent;

        try
        {
            zipContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to return a successful reference
        var expectedReference = new Reference { Id = 124 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert that the result is OK
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Verify the CreatedAtActionResult properties
        var createdResult = result as CreatedAtActionResult;
        Assert.That(createdResult!.Value, Is.TypeOf<Reference>());
        var returnedReference = createdResult.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task ProcessExcel_ReturnsBadRequest_WhenExcelFileIsEmpty()
    {
        SetCurrentUserId(1); // Logist user  

        string testFilePath = Path.Combine(testDataDir, "Register_Empty.xlsx");
        byte[] excelContent;

        try
        {
            excelContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        // Set up the mock processing service to throw InvalidOperationException for empty files
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Excel file is empty"));

        var mockFile = CreateMockFile("Register_Empty.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for an empty Excel file");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenZipFileWithoutExcel()
    {
        // Arrange
        SetCurrentUserId(1); // Set to logist user

        // Load the zip file that doesn't contain any Excel files
        string emptyZipFilePath = Path.Combine(testDataDir, "Zip_Empty.zip");
        byte[] zipContent;

        try
        {
            zipContent = File.ReadAllBytes(emptyZipFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Empty ZIP test file not found at {emptyZipFilePath}: {ex.Message}");
            return;
        }

        // Create a mock file with the zip content
        var mockFile = CreateMockFile("Zip_Empty.zip", "application/zip", zipContent);

        // Act
        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Файл реестра не найден в архиве"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when zip file contains no Excel files");
    }


    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenTextFileUploaded()
    {
        // Arrange
        SetCurrentUserId(1); // Logist user

        // Create or load a text file for testing
        string textFilePath = Path.Combine(testDataDir, "file.txt");
        byte[] textContent;

        try
        {
            // Read the existing file.txt
            textContent = File.ReadAllBytes(textFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {textFilePath}: {ex.Message}");
            return;
        }

        var mockFile = CreateMockFile("file.txt", "text/plain", textContent);

        // Act
        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objResult = result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        var errorMessage = objResult.Value as ErrMessage;
        Assert.That(errorMessage, Is.Not.Null);
        Assert.That(errorMessage!.Msg, Does.Contain("Файлы формата .txt не поддерживаются"));
        Assert.That(errorMessage!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for unsupported file types");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenNullFileUploaded()
    {
        SetCurrentUserId(1); // Logist user

        var result = await _controller.UploadRegister(null!);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for null file");
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenInvalidCompanyIdProvided()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        // Test with an invalid company ID (not WBR or Ozon)
        var result = await _controller.UploadRegister(mockFile.Object, companyId: 999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Неизвестный идентификатор компании [id=999]"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for invalid company ID");
    }

    [Test]
    public async Task UploadRegister_DefaultsToWBRCompany_WhenNoCompanyIdProvided()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 125 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            2, // Should default to WBR ID
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object); // No companyId provided

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));

        // Verify that the processing service was called with WBR ID (2)
        _mockProcessingService.Verify(x => x.UploadRegisterFromExcelAsync(
            2, 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UploadRegister_Returns500InternalServerError_WhenMappingFileNotFound()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        // Setup mock processing service to throw FileNotFoundException
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Mapping file not found", "wbr_register_mapping.yaml"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Не найдена спецификация файла реестра"));
        Assert.That(error!.Msg, Does.Contain("wbr_register_mapping.yaml"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when mapping file is not found");
    }

    [Test]
    public async Task UploadRegister_Returns500InternalServerError_WhenMappingFileNotFound_ForZipFile()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory with an Excel file
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.xlsx");
            using var entryStream = entry.Open();
            byte[] excelContent = System.Text.Encoding.UTF8.GetBytes("fake excel content");
            entryStream.Write(excelContent, 0, excelContent.Length);
        }

        var mockFile = CreateMockFile("test.zip", "application/zip", zipStream.ToArray());

        // Setup mock processing service to throw FileNotFoundException
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Mapping file not found", "ozon_register_mapping.yaml"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Не найдена спецификация файла реестра"));
        Assert.That(error!.Msg, Does.Contain("ozon_register_mapping.yaml"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created when mapping file is not found");
    }

    [Test]
    public async Task UploadRegister_Returns400BadRequest_WhenEmptyExcelInZip()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory with an Excel file
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("empty.xlsx");
            using var entryStream = entry.Open();
            byte[] emptyContent = [];
            entryStream.Write(emptyContent, 0, emptyContent.Length);
        }

        var mockFile = CreateMockFile("empty.zip", "application/zip", zipStream.ToArray());

        // Setup mock processing service to throw InvalidOperationException for empty Excel
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Excel file is empty"));

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Пустой файл реестра"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for empty Excel file in ZIP");
    }


    [TestCase(".doc", "application/msword")]
    [TestCase(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [TestCase(".jpg", "image/jpeg")]
    [TestCase(".png", "image/png")]
    [TestCase(".json", "application/json")]
    [TestCase(".xml", "application/xml")]
    [TestCase(".csv", "text/csv")]
    public async Task UploadRegister_ReturnsBadRequest_ForVariousUnsupportedFileTypes(string extension, string contentType)
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile($"test{extension}", contentType, testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain($"Файлы формата {extension} не поддерживаются"));
        Assert.That(error!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), $"No register should be created for {extension} files");
    }

    [Test]
    public async Task UploadRegister_HandlesFileWithoutExtension()
    {
        SetCurrentUserId(1); // Logist user

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test content");
        var mockFile = CreateMockFile("filename_without_extension", "application/octet-stream", testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Файлы формата  не поддерживаются"));
        Assert.That(error!.Msg, Does.Contain("Можно загрузить .xlsx, .xls, .zip, .rar"));

        // Verify no register was created in the database
        var registersCount = await _dbContext.Registers.CountAsync();
        Assert.That(registersCount, Is.EqualTo(0), "No register should be created for files without extension");
    }


    [Test]
    public async Task UploadRegister_ReturnsSuccess_ForOzonCompany()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 128 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            1, // Ozon ID
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("ozon_test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object, companyId: 1);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));

        // Verify that the processing service was called with Ozon ID (1)
        _mockProcessingService.Verify(x => x.UploadRegisterFromExcelAsync(
            1, 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [Test]
    public async Task UploadRegister_HandlesZipWithNestedDirectories()
    {
        SetCurrentUserId(1); // Logist user

        // Create a ZIP with Excel file in a nested directory
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // Add directory entry (some tools create these)
            archive.CreateEntry("documents/");
            
            // Add Excel file in nested directory
            var entry = archive.CreateEntry("documents/register.xlsx");
            using var entryStream = entry.Open();
            byte[] excelContent = System.Text.Encoding.UTF8.GetBytes("nested excel content");
            entryStream.Write(excelContent, 0, excelContent.Length);
        }

        var expectedReference = new Reference { Id = 130 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            "documents/register.xlsx",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("nested.zip", "application/zip", zipStream.ToArray());

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_HandlesLargeFileUpload()
    {
        SetCurrentUserId(1); // Logist user

        // Create a large mock file (simulate what might happen with large uploads)
        byte[] largeContent = new byte[10 * 1024 * 1024]; // 10MB
        new Random().NextBytes(largeContent);

        var expectedReference = new Reference { Id = 131 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        var mockFile = CreateMockFile("large.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", largeContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result as CreatedAtActionResult;
        var returnedReference = createdResult!.Value as Reference;
        Assert.That(returnedReference!.Id, Is.EqualTo(expectedReference.Id));
    }

    [Test]
    public async Task UploadRegister_HandlesConcurrentUploads()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference1 = new Reference { Id = 132 };
        var expectedReference2 = new Reference { Id = 133 };

        // Setup different responses for different calls
        var setupSequence = _mockProcessingService.SetupSequence(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()));
        
        setupSequence.ReturnsAsync(expectedReference1);
        setupSequence.ReturnsAsync(expectedReference2);

        byte[] content1 = System.Text.Encoding.UTF8.GetBytes("first file content");
        byte[] content2 = System.Text.Encoding.UTF8.GetBytes("second file content");

        var mockFile1 = CreateMockFile("file1.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content1);
        var mockFile2 = CreateMockFile("file2.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content2);

        // Execute concurrent uploads
        var task1 = _controller.UploadRegister(mockFile1.Object);
        var task2 = _controller.UploadRegister(mockFile2.Object);

        var results = await Task.WhenAll(task1, task2);

        // Both should succeed
        Assert.That(results[0], Is.TypeOf<CreatedAtActionResult>());
        Assert.That(results[1], Is.TypeOf<CreatedAtActionResult>());

        var result1 = results[0] as CreatedAtActionResult;
        var result2 = results[1] as CreatedAtActionResult;

        var ref1 = result1!.Value as Reference;
        var ref2 = result2!.Value as Reference;

        Assert.That(ref1!.Id, Is.EqualTo(expectedReference1.Id));
        Assert.That(ref2!.Id, Is.EqualTo(expectedReference2.Id));
    }


    [Test]
    public async Task UploadRegister_LogsDebugInformation()
    {
        SetCurrentUserId(1); // Logist user

        var expectedReference = new Reference { Id = 134 };
        _mockProcessingService.Setup(x => x.UploadRegisterFromExcelAsync(
            It.IsAny<int>(), 
            It.IsAny<byte[]>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReference);

        byte[] testContent = System.Text.Encoding.UTF8.GetBytes("test excel content");
        var mockFile = CreateMockFile("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", testContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Note: In a real scenario, you might want to verify that logging actually occurred
        // This would require setting up a mock logger and verifying log calls
        // For now, we just verify the method executed successfully
    }

    [Test]
    public async Task DeleteRegister_DeletesEmptyRegister_WhenUserIsLogist()
    {
        SetCurrentUserId(1); // Logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx" };

        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Registers.FindAsync(1), Is.Null);
        Assert.That(_dbContext.Orders.Any(o => o.RegisterId == 1), Is.False);
    }

    [Test]
    public async Task DeleteRegister_FailToDeleteRegisterAndOrders_WhenUserIsLogist()
    {
        SetCurrentUserId(1); // Logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx" };
        var order1 = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1 };
        var order2 = new WbrOrder { Id = 2, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Registers.FindAsync(1), Is.Null);
        Assert.That(_dbContext.Orders.Any(o => o.RegisterId == 1), Is.False);

        // Assert.That(result, Is.TypeOf<ObjectResult>());
        // var objResult = result as ObjectResult;
        // Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task DeleteRegister_ReturnsForbidden_WhenUserIsNotLogist()
    {
        SetCurrentUserId(2); // Non-logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteRegister_ReturnsNotFound_WhenRegisterDoesNotExist()
    {
        SetCurrentUserId(1); // Logist user

        var result = await _controller.DeleteRegister(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task PutRegister_UpdatesData_WhenUserIsLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Countries.Add(new Country { IsoNumeric = 100, IsoAlpha2 = "XX", NameRuShort = "XX" });
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem
        {
            InvoiceNumber = "INV",
            InvoiceDate = new DateOnly(2025, 1, 2),
            DestCountryCode = 100,
            TransportationTypeId = 1,
            CustomsProcedureId = 1
        };

        var result = await _controller.PutRegister(1, update);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var saved = await _dbContext.Registers.FindAsync(1);
        Assert.That(saved!.InvoiceNumber, Is.EqualTo("INV"));
        Assert.That(saved.InvoiceDate, Is.EqualTo(new DateOnly(2025, 1, 2)));
        Assert.That(saved.DestCountryCode, Is.EqualTo((short)100));
        Assert.That(saved.TransportationTypeId, Is.EqualTo(1));
        Assert.That(saved.CustomsProcedureId, Is.EqualTo(1));
    }

    [Test]
    public async Task PutRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.PutRegister(1, new RegisterUpdateItem());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PutRegister_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.PutRegister(99, new RegisterUpdateItem());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1);
        _dbContext.Statuses.Add(new OrderStatus { Id = 1, Title = "S" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.SetOrderStatuses(99, 1);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsNotFound_WhenStatusMissing()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.SetOrderStatuses(1, 5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // admin but not logist
        var result = await _controller.SetOrderStatuses(1, 1);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // Helper method to create mock IFormFile objects
    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((stream, token) => {
                stream.Write(content, 0, content.Length);
            })
            .Returns(Task.CompletedTask);

        return mockFile;
    }

    [Test]
    public async Task ValidateRegister_RunsService_ForLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 5, FileName = "r.xlsx" });
        await _dbContext.SaveChangesAsync();

        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.StartValidationAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(handle);

        var result = await _controller.ValidateRegister(5);

        _mockRegValidationService.Verify(s => s.StartValidationAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task ValidateRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.ValidateRegister(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.StartValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegister_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ValidateRegister(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsData()
    {
        SetCurrentUserId(1);
        var progress = new ValidationProgress { HandleId = Guid.NewGuid(), Total = 10, Processed = 5 };
        _mockRegValidationService.Setup(s => s.GetProgress(progress.HandleId)).Returns(progress);

        var result = await _controller.GetValidationProgress(progress.HandleId);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo(progress));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetValidationProgress(Guid.NewGuid());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CancelValidation_ReturnsNoContent()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.CancelValidation(handle)).Returns(true);

        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task CancelValidation_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.CancelValidation(Guid.NewGuid());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.GetValidationProgress(handle);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.GetProgress(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task CancelValidation_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.CancelValidation(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegister_WithRealService_CreatesFeacnLinks()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 200, FileName = "r.xlsx" };
        var feacnOrder = new FeacnOrder { Id = 300, Title = "t" };
        var prefix = new FeacnPrefix { Id = 400, Code = "12", FeacnOrderId = 300, FeacnOrder = feacnOrder };
        var order = new WbrOrder { Id = 201, RegisterId = 200, StatusId = 1, TnVed = "1234567890" };
        _dbContext.Registers.Add(register);
        _dbContext.FeacnOrders.Add(feacnOrder);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var orderValidationService = new OrderValidationService(_dbContext, new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(x => x.GetService(typeof(AppDbContext))).Returns(_dbContext);
        spMock.Setup(x => x.GetService(typeof(IOrderValidationService))).Returns(orderValidationService);
        spMock.Setup(x => x.GetService(typeof(IFeacnPrefixCheckService))).Returns(new FeacnPrefixCheckService(_dbContext));
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // Update the logger type to match the expected type for RegisterValidationService
        var realRegSvc = new RegisterValidationService(_dbContext, scopeFactoryMock.Object, new LoggerFactory().CreateLogger<RegisterValidationService>(), new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, realRegSvc, _mockProcessingService.Object, _mockIndPostGenerator.Object);

        var result = await _controller.ValidateRegister(200);
        var handle = ((GuidReference)((OkObjectResult)result.Result!).Value!).Id;

        // wait for completion
        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            progress = realRegSvc.GetProgress(handle);
            if (progress != null && progress.Finished)
                break;
            await Task.Delay(50);
        }

        var orderReloaded = await _dbContext.Orders.Include(o => o.BaseOrderFeacnPrefixes).FirstAsync(o => o.Id == 201);
        Assert.That(orderReloaded.BaseOrderFeacnPrefixes.Any(l => l.FeacnPrefixId == 400), Is.True);
    }

    [Test]
    public async Task NextOrder_ReturnsNextOrder_AfterGiven()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new OrderCheckStatus { Id = 101, Title = "Has" },
            new OrderCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 201 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextOrder(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(20));
    }

    [Test]
    public async Task NextOrder_PerformsCircularSearch()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new OrderCheckStatus { Id = 101, Title = "Has" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 1 }; // Ozon company
        _dbContext.Registers.Add(reg);
        var ozonOrder1 = new OzonOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        var ozonOrder2 = new OzonOrder { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 201 };
        var ozonOrder3 = new OzonOrder { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        _dbContext.Orders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        _dbContext.OzonOrders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextOrder(3);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task NextOrder_ReturnsNoContent_WhenNoMatches()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new OrderCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 201 });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.NextOrder(1);

        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task NextOrder_ReturnsNotFound_WhenOrderMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.NextOrder(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task NextOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.NextOrder(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PrevOrder_ReturnsPrevOrder_BeforeGiven()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new OrderCheckStatus { Id = 101, Title = "Has" },
            new OrderCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 101 },
            new WbrOrder { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 201 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.PrevOrder(20);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(10));
    }

    [Test]
    public async Task PrevOrder_PerformsCircularSearch()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new OrderCheckStatus { Id = 101, Title = "Has" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 1 }; // Ozon company
        _dbContext.Registers.Add(reg);
        var ozonOrder1 = new OzonOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        var ozonOrder2 = new OzonOrder { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 201 };
        var ozonOrder3 = new OzonOrder { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 101 };
        _dbContext.Orders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        _dbContext.OzonOrders.AddRange(ozonOrder1, ozonOrder2, ozonOrder3);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.PrevOrder(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(3));
    }

    [Test]
    public async Task PrevOrder_ReturnsNoContent_WhenNoMatches()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.Add(new OrderCheckStatus { Id = 201, Title = "Ok" });
        var reg = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 201 });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.PrevOrder(1);

        Assert.That(result.Result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task PrevOrder_ReturnsNotFound_WhenOrderMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.PrevOrder(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task PrevOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.PrevOrder(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DownloadRegister_ReturnsFile_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 10, FileName = "reg.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        byte[] bytes = [1, 2, 3];
        _mockProcessingService.Setup(s => s.DownloadRegisterToExcelAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var result = await _controller.DownloadRegister(10);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = result as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("reg.xlsx"));
        Assert.That(file.ContentType, Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        Assert.That(file.FileContents, Is.EqualTo(bytes));
    }

    [Test]
    public async Task DownloadRegister_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.DownloadRegister(99);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        _mockProcessingService.Verify(s => s.DownloadRegisterToExcelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DownloadRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 11, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DownloadRegister(11);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockProcessingService.Verify(s => s.DownloadRegisterToExcelAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Generate_ReturnsFile_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 20, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        byte[] zip = [1, 2, 3, 4];
        _mockIndPostGenerator.Setup(g => g.GenerateXML4R(20)).ReturnsAsync(("IndPost_r.zip", zip));

        var result = await _controller.Generate(20);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = result as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("IndPost_r.zip"));
        Assert.That(file.ContentType, Is.EqualTo("application/zip"));
        Assert.That(file.FileContents, Is.EqualTo(zip));
    }

    [Test]
    public async Task Generate_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.Generate(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        _mockIndPostGenerator.Verify(g => g.GenerateXML4R(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task Generate_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 21, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Generate(21);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockIndPostGenerator.Verify(g => g.GenerateXML4R(It.IsAny<int>()), Times.Never);
    }

}

