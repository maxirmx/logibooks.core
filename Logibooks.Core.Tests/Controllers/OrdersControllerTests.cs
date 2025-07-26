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

using System;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NUnit.Framework;
using Moq;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using AutoMapper;
using System.Collections.Generic;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class OrdersControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IOrderValidationService> _mockValidationService;
    private IMorphologySearchService _morphologyService;
    private Mock<IRegisterProcessingService> _mockProcessingService;
    private Mock<IOrderIndPostGenerator> _mockIndPostGenerator;
    private ILogger<OrdersController> _logger;
    private IUserInformationService _userService;
    private OrdersController _controller;
    private Role _logistRole;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orders_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _logistRole = new Role { Id = 1, Name = "logist", Title = "Логист" };
        _dbContext.Roles.Add(_logistRole);

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
        _dbContext.Users.Add(_logistUser);
        _dbContext.Companies.AddRange(
            new Company { Id = 1, Inn = "1", Name = "Ozon" },
            new Company { Id = 2, Inn = "2", Name = "WBR" }
        );
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<OrdersController>();
        _mockValidationService = new Mock<IOrderValidationService>();
        _mockProcessingService = new Mock<IRegisterProcessingService>();
        _mockIndPostGenerator = new Mock<IOrderIndPostGenerator>();
        // Note: Cannot mock static methods GetWBRId() and GetOzonId() - they return constants
        _morphologyService = new MorphologySearchService();
        _userService = new UserInformationService(_dbContext);
        _controller = CreateController();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private OrdersController CreateController()
    {
        var mockMapper = new Mock<IMapper>();
        return new OrdersController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            mockMapper.Object,
            _mockValidationService.Object,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object);
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = CreateController();
    }

    [Test]
    public async Task GetOrder_ReturnsOrder_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" };
        var sw = new StopWord { Id = 5, Word = "bad" };
        var link = new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 5, BaseOrder = order, StopWord = sw };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        _dbContext.StopWords.Add(sw);
        _dbContext.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<OrderViewItem>());
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value.StopWordIds.Count, Is.EqualTo(1));
        Assert.That(result.Value.StopWordIds.First(), Is.EqualTo(5));
    }

    [Test]
    public async Task UpdateOrder_ChangesData()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "A" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        // Configure the mock to perform the actual mapping
        var mockMapper = new Mock<IMapper>();
        mockMapper.Setup(m => m.Map(It.IsAny<OrderUpdateItem>(), It.IsAny<WbrOrder>()))
            .Callback<OrderUpdateItem, WbrOrder>((src, dest) =>
            {
                // Simulate the AutoMapper behavior - only update non-null values
                if (src.StatusId.HasValue) dest.StatusId = src.StatusId.Value;
                if (src.TnVed != null) dest.TnVed = src.TnVed;
                // Add other properties as needed for testing
            });

        _controller = new OrdersController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            mockMapper.Object,
            _mockValidationService.Object,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object
        );

        var result = await _controller.UpdateOrder(2, updated);

        Assert.That(result, Is.TypeOf<NoContentResult>());

        var saved = await _dbContext.Orders.FindAsync(2);
        Assert.That(saved!.StatusId, Is.EqualTo(2));
        Assert.That(saved.TnVed, Is.EqualTo("B"));
    }

    [Test]
    public async Task UpdateOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var updated = new OrderUpdateItem();

        var result = await _controller.UpdateOrder(1, updated);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateOrder_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        var result = await _controller.UpdateOrder(1, updated);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateOrder_ReturnsNotFound_WhenCompanyNotFound()
    {
        SetCurrentUserId(1);
        // Create an order with a register that references a non-existent company
        var register = new Register { Id = 1, CompanyId = 999, FileName = "r.xlsx" }; // Company 999 doesn't exist
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        var result = await _controller.UpdateOrder(1, updated);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateOrder_ReturnsNotFound_WhenOrderExistsInWrongTable()
    {
        SetCurrentUserId(1);
        // Create a WBR register but try to update an order that doesn't exist in WBR table
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" }; // CompanyId = 2 is WBR
        var ozonOrder = new OzonOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" }; // Order exists in Ozon table
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(ozonOrder);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        var result = await _controller.UpdateOrder(1, updated);

        // Should return 404 because the order is in the wrong table for the company type
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateOrder_UpdatesWbrOrder_WhenCompanyIsWBR()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" }; // CompanyId = 2 is WBR
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A", OrderNumber = "WBR123" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var mockMapper = new Mock<IMapper>();
        mockMapper.Setup(m => m.Map(It.IsAny<OrderUpdateItem>(), It.IsAny<WbrOrder>()))
            .Callback<OrderUpdateItem, WbrOrder>((src, dest) =>
            {
                if (src.StatusId.HasValue) dest.StatusId = src.StatusId.Value;
                if (src.OrderNumber != null) dest.OrderNumber = src.OrderNumber;
            });

        _controller = new OrdersController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            mockMapper.Object,
            _mockValidationService.Object,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object
        );

        var updated = new OrderUpdateItem { StatusId = 3, OrderNumber = "WBR456" };
        var result = await _controller.UpdateOrder(1, updated);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var savedOrder = await _dbContext.WbrOrders.FindAsync(1);
        Assert.That(savedOrder!.StatusId, Is.EqualTo(3));
        Assert.That(savedOrder.OrderNumber, Is.EqualTo("WBR456"));
    }

    [Test]
    public async Task UpdateOrder_UpdatesOzonOrder_WhenCompanyIsOzon()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 2, CompanyId = 1, FileName = "r.xlsx" }; // CompanyId = 1 is Ozon
        var order = new OzonOrder { Id = 2, RegisterId = 2, StatusId = 1, TnVed = "B", OzonId = "OZON456" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var mockMapper = new Mock<IMapper>();
        mockMapper.Setup(m => m.Map(It.IsAny<OrderUpdateItem>(), It.IsAny<OzonOrder>()))
            .Callback<OrderUpdateItem, OzonOrder>((src, dest) =>
            {
                if (src.StatusId.HasValue) dest.StatusId = src.StatusId.Value;
                if (src.PostingNumber != null) dest.PostingNumber = src.PostingNumber;
            });

        _controller = new OrdersController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            mockMapper.Object,
            _mockValidationService.Object,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object
        );

        var updated = new OrderUpdateItem { StatusId = 3, PostingNumber = "POST123" };
        var result = await _controller.UpdateOrder(2, updated);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var savedOrder = await _dbContext.OzonOrders.FindAsync(2);
        Assert.That(savedOrder!.StatusId, Is.EqualTo(3));
        Assert.That(savedOrder.PostingNumber, Is.EqualTo("POST123"));
    }

     [Test]
    public async Task GetOrders_FiltersAndSorts()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 2, TnVed = "A" },
            new WbrOrder { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "B" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1, statusId: 2, tnVed: "B", sortBy: "tnVed", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;

        Assert.That(pr!.Items.Count(), Is.EqualTo(1));
        Assert.That(pr.Items.First().Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetOrders_ReturnsStopWords()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var sw = new StopWord { Id = 7, Word = "foo" };
        var o1 = new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1 };
        var link = new BaseOrderStopWord { BaseOrderId = 10, StopWordId = 7, BaseOrder = o1, StopWord = sw };
        _dbContext.Registers.Add(reg);
        _dbContext.StopWords.Add(sw);
        _dbContext.Orders.Add(o1);
        _dbContext.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1);
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;

        Assert.That(pr!.Items.First().StopWordIds.Count, Is.EqualTo(1));
        Assert.That(pr.Items.First().StopWordIds.First(), Is.EqualTo(7));
    }

    [Test]
    public async Task GetOrders_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        _dbContext.SaveChanges();

        var result = await _controller.GetOrders(registerId: 1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var result = await _controller.GetOrder(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetOrder_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetOrder_ReturnsNotFound_WhenCompanyNotFound()
    {
        SetCurrentUserId(1);
        // Create an order with a register that references a non-existent company
        var register = new Register { Id = 1, CompanyId = 999, FileName = "r.xlsx" }; // Company 999 doesn't exist
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetOrders_InvalidPagination_ReturnsBadRequest()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetOrders(registerId: 1, page: 0);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetOrders_InvalidSortBy_ReturnsBadRequest()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetOrders(registerId: 1, sortBy: "foo");

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetOrders_InvalidSortOrder_ReturnsBadRequest()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetOrders(registerId: 1, sortOrder: "bad");

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetOrders_ReturnsAll_WhenPageSizeIsMinusOne()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new WbrOrder { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1, pageSize: -1);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;
        Assert.That(pr!.Items.Count(), Is.EqualTo(2));
        Assert.That(pr.Pagination.TotalCount, Is.EqualTo(2));
        Assert.That(pr.Pagination.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public async Task GetOrders_PageExceedsTotalPages_ResetsToFirstPage()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        for (int i = 1; i <= 6; i++)
        {
            _dbContext.Orders.Add(new WbrOrder { Id = i, RegisterId = 1, StatusId = 1, TnVed = "A" });
        }
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1, page: 3, pageSize: 5);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;
        Assert.That(pr!.Pagination.CurrentPage, Is.EqualTo(1));
        Assert.That(pr.Items.First().Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetCheckStatuses_ReturnsAllCheckStatuses()
    {
        // Arrange
        var statuses = new List<OrderCheckStatus>
        {
            new() { Id = 1, Title = "Loaded" },
            new() { Id = 101, Title = "Problem" },
            new() { Id = 201, Title = "Verified" }
        };
        _dbContext.CheckStatuses.AddRange(statuses);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetCheckStatuses();

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());

        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult!.Value, Is.Not.Null);

        var returnedStatuses = okResult.Value as IEnumerable<OrderCheckStatus>;
        Assert.That(returnedStatuses, Is.Not.Null);
        Assert.That(returnedStatuses!.Count(), Is.EqualTo(3));

        if (returnedStatuses is null) return;

        var statusList = returnedStatuses.ToList();
        Assert.That(statusList[0].Id, Is.EqualTo(1));
        Assert.That(statusList[1].Id, Is.EqualTo(101));
        Assert.That(statusList[2].Id, Is.EqualTo(201));
    }

    [Test]
    public async Task GetCheckStatuses_ReturnsEmptyList_WhenNoStatusesExist()
    {
        // Act
        var result = await _controller.GetCheckStatuses();

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());

        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult!.Value, Is.Not.Null);

        var returnedStatuses = okResult.Value as IEnumerable<OrderCheckStatus>;
        Assert.That(returnedStatuses, Is.Not.Null);
        Assert.That(returnedStatuses!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetCheckStatuses_OrdersStatusesByIdAscending()
    {
        // Arrange - add in non-sequential order
        var statuses = new List<OrderCheckStatus>
        {
            new() { Id = 201, Title = "Verified" },
            new() { Id = 1, Title = "Loaded" },
            new() { Id = 101, Title = "Problem" }
        };
        _dbContext.CheckStatuses.AddRange(statuses);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetCheckStatuses();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var returnedStatuses = okResult!.Value as IEnumerable<OrderCheckStatus>;
        var statusList = returnedStatuses!.ToList();

        // Verify ordering by Id
        Assert.That(statusList[0].Id, Is.EqualTo(1));
        Assert.That(statusList[1].Id, Is.EqualTo(101));
        Assert.That(statusList[2].Id, Is.EqualTo(201));
    }

    [Test]
    public async Task GetCheckStatuses_DoesNotRequireAuthorization()
    {
        // Arrange - don't set any user in HttpContext
        // This ensures the method works without checking user roles
        var statuses = new List<OrderCheckStatus>
        {
            new OrderCheckStatus { Id = 1, Title = "Status" }
        };
        _dbContext.CheckStatuses.AddRange(statuses);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetCheckStatuses();

        // Assert - should work without any auth checks
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var returnedStatuses = okResult!.Value as IEnumerable<OrderCheckStatus>;
        Assert.That(returnedStatuses!.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetCheckStatuses_ReturnsCompleteOrderCheckStatusObjects()
    {
        // Arrange
        var status = new OrderCheckStatus { Id = 42, Title = "Test Status" };
        _dbContext.CheckStatuses.Add(status);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetCheckStatuses();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var returnedStatuses = okResult!.Value as IEnumerable<OrderCheckStatus>;
        var returnedStatus = returnedStatuses!.First();

        // Verify all properties are returned correctly
        Assert.That(returnedStatus.Id, Is.EqualTo(42));
        Assert.That(returnedStatus.Title, Is.EqualTo("Test Status"));
    }

    [Test]
    public async Task GetOrderStatus_ReturnsStatusTitle_WhenOrderExists()
    {
        // Arrange
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        var status = new OrderStatus { Id = 1, Title = "Test Status" };
        var order = new WbrOrder { Shk = "12345678", RegisterId = 1, StatusId = 1, Status = status };
        _dbContext.Statuses.Add(status);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act: Retrieve the order status for an existing order and verify the returned status title
        var result = await _controller.GetOrderStatus("12345678");

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo("Test Status"));
    }

    [Test]
    public async Task GetOrderStatus_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        // Act
        var result = await _controller.GetOrderStatus("nonexistent");

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));

        var error = objResult.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("nonexistent"));
    }

    [Test]
    public async Task GetOrderStatusWorksWithoutAuthentication()
    {
        // Arrange
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        var status = new OrderStatus { Id = 1, Title = "Available" };
        var order = new WbrOrder { Shk = "ABC123", RegisterId = 1,  StatusId = 1, Status = status };
        _dbContext.Statuses.Add(status);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Don't set any user ID - this tests that [AllowAnonymous] works
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _controller.GetOrderStatus("ABC123");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo("Available"));
    }

    [Test]
    public async Task GetOrderStatus_HandlesNullShk()
    {
        // Act
        var result = await _controller.GetOrderStatus(null!);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ValidateOrder_RunsService_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 10, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 10, RegisterId = 10, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.ValidateOrder(10);

        _mockValidationService.Verify(s => s.ValidateAsync(
            order,
            It.IsAny<MorphologyContext>(),
            It.IsAny<StopWordsContext>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task ValidateOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99);
        var result = await _controller.ValidateOrder(1);

        _mockValidationService.Verify(s => s.ValidateAsync(
            It.IsAny<BaseOrder>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<StopWordsContext>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ValidateOrder_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ValidateOrder(99);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        _mockValidationService.Verify(s => s.ValidateAsync(
            It.IsAny<BaseOrder>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<StopWordsContext>(),
            It.IsAny<FeacnPrefixCheckContext?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ValidateOrder_WithRealService_CreatesFeacnLinks()
    {
        SetCurrentUserId(1);

        var register = new Register { Id = 20, CompanyId = 2, FileName = "r.xlsx" };
        var feacnOrder = new FeacnOrder { Id = 30, Title = "t" };
        var prefix = new FeacnPrefix { Id = 40, Code = "12", FeacnOrderId = 30, FeacnOrder = feacnOrder };
        var order = new WbrOrder { Id = 20, RegisterId = 20, StatusId = 1, TnVed = "1234567890" };
        _dbContext.Registers.Add(register);
        _dbContext.FeacnOrders.Add(feacnOrder);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var validationSvc = new OrderValidationService(_dbContext, new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = 1;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);

        var ctrl = new OrdersController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            new Mock<IMapper>().Object,
            validationSvc,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object);

        await ctrl.ValidateOrder(20);
        var res = await ctrl.GetOrder(20);

        Assert.That(res.Value!.FeacnOrderIds, Does.Contain(30));
    }

    [Test]
    public async Task GetOrder_ReturnsFeacnOrderIds_WithUniqueValues()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var feacnOrder1 = new FeacnOrder { Id = 10, Title = "Order 1" };
        var feacnOrder2 = new FeacnOrder { Id = 20, Title = "Order 2" };
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" };
        
        // Create multiple prefixes - some with the same FeacnOrderId and some with different FeacnOrderIds
        var prefix1 = new FeacnPrefix { Id = 100, Code = "12", FeacnOrderId = 10, FeacnOrder = feacnOrder1 };
        var prefix2 = new FeacnPrefix { Id = 101, Code = "34", FeacnOrderId = 10, FeacnOrder = feacnOrder1 }; // Same order
        var prefix3 = new FeacnPrefix { Id = 102, Code = "56", FeacnOrderId = 20, FeacnOrder = feacnOrder2 }; // Different order
        
        // Create links to all prefixes
        var link1 = new BaseOrderFeacnPrefix { BaseOrderId = 1, FeacnPrefixId = 100, BaseOrder = order, FeacnPrefix = prefix1 };
        var link2 = new BaseOrderFeacnPrefix { BaseOrderId = 1, FeacnPrefixId = 101, BaseOrder = order, FeacnPrefix = prefix2 };
        var link3 = new BaseOrderFeacnPrefix { BaseOrderId = 1, FeacnPrefixId = 102, BaseOrder = order, FeacnPrefix = prefix3 };

        _dbContext.Registers.Add(register);
        _dbContext.FeacnOrders.AddRange(feacnOrder1, feacnOrder2);
        _dbContext.FeacnPrefixes.AddRange(prefix1, prefix2, prefix3);
        _dbContext.Orders.Add(order);
        _dbContext.Set<BaseOrderFeacnPrefix>().AddRange(link1, link2, link3);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.FeacnOrderIds.Count, Is.EqualTo(2)); // Should have only 2 unique FeacnOrder IDs
        Assert.That(result.Value.FeacnOrderIds, Does.Contain(10));
        Assert.That(result.Value.FeacnOrderIds, Does.Contain(20));
    }

    [Test]
    public async Task GetOrders_ReturnsFeacnOrderIds()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var feacnOrder = new FeacnOrder { Id = 25, Title = "FEACN Order" };
        var prefix = new FeacnPrefix { Id = 50, Code = "78", FeacnOrderId = 25, FeacnOrder = feacnOrder };
        var order = new WbrOrder { Id = 10, RegisterId = 1, StatusId = 1 };
        var link = new BaseOrderFeacnPrefix { BaseOrderId = 10, FeacnPrefixId = 50, BaseOrder = order, FeacnPrefix = prefix };
        
        _dbContext.Registers.Add(reg);
        _dbContext.FeacnOrders.Add(feacnOrder);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.Orders.Add(order);
        _dbContext.Set<BaseOrderFeacnPrefix>().Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1);
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;

        Assert.That(pr!.Items.First().FeacnOrderIds.Count, Is.EqualTo(1));
        Assert.That(pr.Items.First().FeacnOrderIds.First(), Is.EqualTo(25));
    }

    [Test]
    public async Task DeleteOrder_RemovesOrder()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 5, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteOrder(5);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Orders.FindAsync(5), Is.Null);
    }

    [Test]
    public async Task DeleteOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 5, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteOrder(5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        Assert.That(await _dbContext.Orders.FindAsync(5), Is.Not.Null); // Order should still exist
    }

    [Test]
    public async Task DeleteOrder_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        SetCurrentUserId(1);
        var result = await _controller.DeleteOrder(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DeleteOrder_ReturnsNotFound_WhenCompanyDoesNotExist()
    {
        SetCurrentUserId(1);
        // Create an order with a register that references a non-existent company
        var register = new Register { Id = 1, CompanyId = 999, FileName = "r.xlsx" }; // Company 999 doesn't exist
        var order = new WbrOrder { Id = 5, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteOrder(5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(await _dbContext.Orders.FindAsync(5), Is.Not.Null); // Order should still exist
    }

    [Test]
    public async Task GetOrder_ReturnsWbrOrder_WhenCompanyIsWBR()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" }; // CompanyId = 2 is WBR
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A", OrderNumber = "WBR123" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<OrderViewItem>());
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value.OrderNumber, Is.EqualTo("WBR123")); // WbrOrder specific field
        Assert.That(result.Value.OzonId, Is.Null); // Should not have Ozon-specific fields
    }

    [Test]
    public async Task GetOrder_ReturnsOzonOrder_WhenCompanyIsOzon()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 2, CompanyId = 1, FileName = "r.xlsx" }; // CompanyId = 1 is Ozon
        var order = new OzonOrder { Id = 2, RegisterId = 2, StatusId = 1, TnVed = "B", OzonId = "OZON456", PostingNumber = "POST789" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(2);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<OrderViewItem>());
        Assert.That(result.Value!.Id, Is.EqualTo(2));
        Assert.That(result.Value.OzonId, Is.EqualTo("OZON456")); // OzonOrder specific field
        Assert.That(result.Value.PostingNumber, Is.EqualTo("POST789")); // OzonOrder specific field
        Assert.That(result.Value.OrderNumber, Is.Null); // Should not have WBR-specific fields
    }

    [Test]
    public async Task GetOrder_ReturnsNotFound_WhenOrderExistsInWrongTable()
    {
        SetCurrentUserId(1);
        // Create a WBR register but try to find an order that doesn't exist in WBR table
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" }; // CompanyId = 2 is WBR
        var ozonOrder = new OzonOrder { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" }; // Order exists in Ozon table
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(ozonOrder);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        // Should return 404 because the order is in the wrong table for the company type
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Generate_ReturnsFile_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 5, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 5, RegisterId = 5, StatusId = 1, Shk = "123" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        _mockIndPostGenerator.Setup(x => x.GenerateXML(5)).ReturnsAsync(("IndPost_123.xml", "<AltaIndPost />"));

        var result = await _controller.Generate(5);

        Assert.That(result, Is.TypeOf<FileContentResult>());
        var file = result as FileContentResult;
        Assert.That(file!.FileDownloadName, Is.EqualTo("IndPost_123.xml"));
        Assert.That(file.ContentType, Is.EqualTo("application/xml"));
    }

    [Test]
    public async Task Generate_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99);
        var result = await _controller.Generate(1);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Generate_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.Generate(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
    public async Task ApproveOrder_SetsCheckStatusToApproved_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 100, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        var result = await _controller.ApproveOrder(100);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        var saved = await _dbContext.Orders.FindAsync(100);
        Assert.That(saved!.CheckStatusId, Is.EqualTo(301));
    }

    [Test]
    public async Task ApproveOrder_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        var order = new WbrOrder { Id = 101, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.ApproveOrder(101);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        var saved = await _dbContext.Orders.FindAsync(101);
        Assert.That(saved!.CheckStatusId, Is.EqualTo(1)); // Should not change
    }

    [Test]
    public async Task ApproveOrder_ReturnsNotFound_WhenOrderMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ApproveOrder(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
