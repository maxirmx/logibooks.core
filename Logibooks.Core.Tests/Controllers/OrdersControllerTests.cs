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

using System.Threading.Tasks;
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

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class OrdersControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<OrdersController> _logger;
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
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<OrdersController>();
        var mockMapper = new Mock<IMapper>(); // Add this line to mock the IMapper dependency  
        _controller = new OrdersController(_mockHttpContextAccessor.Object, _dbContext, _logger, mockMapper.Object); // Pass the mockMapper.Object  
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
        var mockMapper = new Mock<IMapper>();
        _controller = new OrdersController(_mockHttpContextAccessor.Object, _dbContext, _logger, mockMapper.Object);
    }

    [Test]
    public async Task GetOrder_ReturnsOrder_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        var order = new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<OrderViewItem>());
        Assert.That(result.Value!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateOrder_ChangesData()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        var order = new Order { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "A" };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        // Configure the mock to perform the actual mapping
        var mockMapper = new Mock<IMapper>();
        mockMapper.Setup(m => m.Map(It.IsAny<OrderUpdateItem>(), It.IsAny<Order>()))
            .Callback<OrderUpdateItem, Order>((src, dest) =>
            {
                // Simulate the AutoMapper behavior - only update non-null values
                if (src.StatusId.HasValue) dest.StatusId = src.StatusId.Value;
                if (src.TnVed != null) dest.TnVed = src.TnVed;
                // Add other properties as needed for testing
            });

        // Create a new controller instance with the properly configured mock
        _controller = new OrdersController(_mockHttpContextAccessor.Object, _dbContext, _logger, mockMapper.Object);

        var result = await _controller.UpdateOrder(2, updated);

        Assert.That(result, Is.TypeOf<NoContentResult>());

        var saved = await _dbContext.Orders.FindAsync(2);
        Assert.That(saved!.StatusId, Is.EqualTo(2));
        Assert.That(saved.TnVed, Is.EqualTo("B"));
    }

    [Test]
    public async Task GetOrders_FiltersAndSorts()
    {
        SetCurrentUserId(1);
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new Order { Id = 2, RegisterId = 1, StatusId = 2, TnVed = "A" },
            new Order { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "B" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrders(registerId: 1, statusId: 2, tnVed: "B", sortBy: "tnVed", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<OrderViewItem>;

        Assert.That(pr!.Items.Count(), Is.EqualTo(1));
        Assert.That(pr.Items.First().Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetOrders_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // unknown user
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
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
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrder(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
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
        var register = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var updated = new OrderUpdateItem { StatusId = 2, TnVed = "B" };

        var result = await _controller.UpdateOrder(1, updated);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
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
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.AddRange(
            new Order { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new Order { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" }
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
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
        _dbContext.Registers.Add(reg);
        for (int i = 1; i <= 6; i++)
        {
            _dbContext.Orders.Add(new Order { Id = i, RegisterId = 1, StatusId = 1, TnVed = "A" });
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
    public async Task GetOrderStatus_ReturnsTitle_WhenExists()
    {
        var status = new OrderStatus { Id = 1, Title = "Loaded" };
        _dbContext.Statuses.Add(status);
        var reg = new Register { Id = 1, FileName = "r.xlsx" };
        var order = new Order { Id = 1, RegisterId = 1, StatusId = 1, OrderNumber = "A1" };
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetOrderStatus("A1");

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo("Loaded"));
    }

    [Test]
    public async Task GetOrderStatus_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetOrderStatus("NO");

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetCheckStatuses_ReturnsAllCheckStatuses()
    {
        // Arrange
        var statuses = new List<OrderCheckStatus>
        {
            new OrderCheckStatus { Id = 1, Title = "Loaded" },
            new OrderCheckStatus { Id = 101, Title = "Problem" },
            new OrderCheckStatus { Id = 201, Title = "Verified" }
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
            new OrderCheckStatus { Id = 201, Title = "Verified" },
            new OrderCheckStatus { Id = 1, Title = "Loaded" },
            new OrderCheckStatus { Id = 101, Title = "Problem" }
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
}
