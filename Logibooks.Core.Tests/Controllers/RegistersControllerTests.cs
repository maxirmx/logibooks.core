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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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
using Microsoft.Extensions.Logging;

using NUnit.Framework;
using Moq;
using System.Threading.Tasks;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using System.IO;
using System.Threading;
using System;
using System.Linq;
using System.Reflection;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class RegistersControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<RegistersController> _logger;
    private RegistersController _controller;
    private Role _logistRole;
    private Role _adminRole;
    private User _logistUser;
    private User _adminUser;
#pragma warning restore CS8618

    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");

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
        _logger = new LoggerFactory().CreateLogger<RegistersController>();
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _logger);


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
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _dbContext.Statuses.AddRange(
            new OrderStatus { Id = 1, Name = "loaded", Title = "Loaded" },
            new OrderStatus { Id = 2, Name = "processed", Title = "Processed" }
        );
        var r1 = new Register { Id = 1, FileName = "r1.xlsx" };
        var r2 = new Register { Id = 2, FileName = "r2.xlsx" };
        _dbContext.Registers.AddRange(r1, r2);
        _dbContext.Orders.AddRange(
            new Order { Id = 1, RegisterId = 1, StatusId = 1 },
            new Order { Id = 2, RegisterId = 1, StatusId = 2 },
            new Order { Id = 3, RegisterId = 2, StatusId = 2 }
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
            new Register { Id = 1, FileName = "r1.xlsx" },
            new Register { Id = 2, FileName = "r2.xlsx" }
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
    public async Task GetRegisters_SortsByFileName_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "a.xlsx" },
            new Register { Id = 2, FileName = "b.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(sortBy: "fileName", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].FileName, Is.EqualTo("b.xlsx"));
        Assert.That(items[1].FileName, Is.EqualTo("a.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersResults()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "report1.xlsx" },
            new Register { Id = 2, FileName = "other.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(search: "report");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;

        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("report1.xlsx"));
    }

    [Test]
    public async Task GetRegisters_ReturnsPaginationMetadata()
    {
        SetCurrentUserId(1);
        for (int i = 1; i <= 25; i++)
        {
            _dbContext.Registers.Add(new Register { Id = i, FileName = $"r{i}.xlsx" });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(page: 2, pageSize: 10);
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;

        Assert.That(pr!.Items.Count(), Is.EqualTo(10));
        Assert.That(pr.Pagination.TotalCount, Is.EqualTo(25));
        Assert.That(pr.Pagination.TotalPages, Is.EqualTo(3));
        Assert.That(pr.Pagination.HasNextPage, Is.True);
        Assert.That(pr.Pagination.HasPreviousPage, Is.True);
    }

    [Test]
    public async Task GetRegisters_SortsByOrdersTotalAcrossPages()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx" },
            new Register { Id = 2, FileName = "r2.xlsx" },
            new Register { Id = 3, FileName = "r3.xlsx" },
            new Register { Id = 4, FileName = "r4.xlsx" }
        );
        _dbContext.Orders.AddRange(
            new Order { RegisterId = 1, StatusId = 1 },
            new Order { RegisterId = 2, StatusId = 1 },
            new Order { RegisterId = 2, StatusId = 1 },
            new Order { RegisterId = 3, StatusId = 1 },
            new Order { RegisterId = 3, StatusId = 1 },
            new Order { RegisterId = 3, StatusId = 1 }
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

    [Test]
    public async Task GetRegister_ReturnsRegister_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "reg.xlsx" };
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
        _dbContext.Statuses.AddRange(
            new OrderStatus { Id = 1, Name = "loaded", Title = "Loaded" },
            new OrderStatus { Id = 2, Name = "processed", Title = "Processed" }
        );
        var register = new Register { Id = 1, FileName = "reg.xlsx" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new Order { Id = 1, RegisterId = 1, StatusId = 1 },
            new Order { Id = 2, RegisterId = 1, StatusId = 2 },
            new Order { Id = 3, RegisterId = 1, StatusId = 1 }
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
        _dbContext.Statuses.AddRange(
            new OrderStatus { Id = 1, Name = "loaded", Title = "Loaded" },
            new OrderStatus { Id = 2, Name = "processed", Title = "Processed" },
            new OrderStatus { Id = 3, Name = "delivered", Title = "Delivered" }
        );
        var register = new Register { Id = 1, FileName = "reg.xlsx" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new Order { Id = 1, RegisterId = 1, StatusId = 1 },
            new Order { Id = 2, RegisterId = 1, StatusId = 2 },
            new Order { Id = 3, RegisterId = 1, StatusId = 3 },
            new Order { Id = 4, RegisterId = 1, StatusId = 3 }
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
        _dbContext.Registers.Add(new Register { Id = 1, FileName = "reg.xlsx" });
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
            new Register { Id = 1, FileName = "r1.xlsx" },
            new Register { Id = 2, FileName = "r2.xlsx" },
            new Register { Id = 3, FileName = "r3.xlsx" }
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
            _dbContext.Registers.Add(new Register { Id = i, FileName = $"r{i}.xlsx" });
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
    public async Task GetRegisters_HandlesCaseInsensitiveSortBy()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "a.xlsx" },
            new Register { Id = 2, FileName = "b.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(sortBy: "FILENAME", sortOrder: "desc");
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].FileName, Is.EqualTo("b.xlsx"));
        Assert.That(items[1].FileName, Is.EqualTo("a.xlsx"));
    }

    [Test]
    public async Task GetRegisters_HandlesCaseInsensitiveSortOrder()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "a.xlsx" },
            new Register { Id = 2, FileName = "b.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(sortBy: "filename", sortOrder: "DESC");
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].FileName, Is.EqualTo("b.xlsx"));
        Assert.That(items[1].FileName, Is.EqualTo("a.xlsx"));
    }

    [Test]
    public async Task GetRegisters_DefaultsToAscendingWhenSortOrderIsEmpty()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 2, FileName = "b.xlsx" },
            new Register { Id = 1, FileName = "a.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(sortBy: "id", sortOrder: "");
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegisters_DefaultsToIdWhenSortByIsNull()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 2, FileName = "b.xlsx" },
            new Register { Id = 1, FileName = "a.xlsx" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters(sortBy: null);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenMultipleParametersInvalid()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(page: -1, pageSize: 0, sortBy: "invalid", sortOrder: "wrong");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
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

        var mockFile = CreateMockFile("Реестр_207730349.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());

        // Verify that orders were created in the database
        Assert.That(_dbContext.Orders.Count(), Is.GreaterThan(0),
            "Orders should have been created in the database");
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

        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);

        var result = await _controller.UploadRegister(mockFile.Object);

        // Assert that the result is OK
        Assert.That(result, Is.TypeOf<CreatedAtActionResult>());


        Assert.That(_dbContext.Orders.Count(), Is.GreaterThan(0));
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
    public async Task DeleteRegister_DeletesRegisterAndOrders_WhenUserIsLogist()
    {
        SetCurrentUserId(1); // Logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx" };
        var order1 = new Order { Id = 1, RegisterId = 1, StatusId = 1 };
        var order2 = new Order { Id = 2, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Registers.FindAsync(1), Is.Null);
        Assert.That(_dbContext.Orders.Any(o => o.RegisterId == 1), Is.False);
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
}
