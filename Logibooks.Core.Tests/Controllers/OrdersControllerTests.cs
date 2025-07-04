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
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _logistRole } ]
        };
        _dbContext.Users.Add(_logistUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<OrdersController>();
        _controller = new OrdersController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new OrdersController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
}

