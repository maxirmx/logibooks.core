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
using Microsoft.Extensions.Logging;

using NUnit.Framework;
using Moq;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnInsertItemsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<FeacnInsertItemsController> _logger;
    private IUserInformationService _userService;
    private FeacnInsertItemsController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
    private User _otherUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacn_insert_items_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Администратор" };
        _logistRole = new Role { Id = 2, Name = "logist", Title = "Логист" };
        _dbContext.Roles.AddRange(_adminRole, _logistRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _adminRole } ]
        };
        _logistUser = new User
        {
            Id = 2,
            Email = "logist@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 2, RoleId = 2, Role = _logistRole } ]
        };
        _otherUser = new User
        {
            Id = 3,
            Email = "user@example.com",
            Password = hpw
        };
        _dbContext.Users.AddRange(_adminUser, _logistUser, _otherUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<FeacnInsertItemsController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new FeacnInsertItemsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
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
        _controller = new FeacnInsertItemsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
    }

    [Test]
    public async Task GetItems_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.FeacnInsertItems.AddRange(
            new FeacnInsertItem { Code = "1234567890" },
            new FeacnInsertItem { Code = "0987654321" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetItems();
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForLogist()
    {
        SetCurrentUserId(2);
        var dto = new FeacnInsertItemDto { Code = "1234567890" };
        var result = await _controller.CreateItem(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new FeacnInsertItemDto { Code = "1234567890", InsBefore = "111", InsAfter = "222" };
        var created = await _controller.CreateItem(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as FeacnInsertItemDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.InsBefore = "333";
        var upd = await _controller.UpdateItem(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteItem(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task GetItem_ReturnsItem_ForLogist()
    {
        SetCurrentUserId(2);
        var item = new FeacnInsertItem { Code = "1111111111" };
        _dbContext.FeacnInsertItems.Add(item);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetItem(item.Id);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1111111111"));
    }

    [Test]
    public async Task GetItem_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(2);
        var result = await _controller.GetItem(99);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That((result.Result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetItems_ReturnsForbidden_ForUnauthorizedUser()
    {
        SetCurrentUserId(3);
        var result = await _controller.GetItems();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That((result.Result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Create_ReturnsBadRequest_ForInvalidCode()
    {
        SetCurrentUserId(1);
        var dto = new FeacnInsertItemDto { Code = "123" };
        var result = await _controller.CreateItem(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That((result.Result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenCodeExists()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnInsertItems.Add(new FeacnInsertItem { Code = "2222222222" });
        await _dbContext.SaveChangesAsync();
        var dto = new FeacnInsertItemDto { Code = "2222222222" };
        var result = await _controller.CreateItem(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That((result.Result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Update_ReturnsBadRequest_ForInvalidCode()
    {
        SetCurrentUserId(1);
        var dto = new FeacnInsertItemDto { Id = 1, Code = "abc" };
        var result = await _controller.UpdateItem(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That((result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Update_ReturnsNotFound_WhenItemMissing()
    {
        SetCurrentUserId(1);
        var dto = new FeacnInsertItemDto { Id = 5, Code = "4444444444" };
        var result = await _controller.UpdateItem(5, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That((result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Update_ReturnsConflict_WhenDuplicateCode()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnInsertItems.AddRange(
            new FeacnInsertItem { Id = 1, Code = "5555555555" },
            new FeacnInsertItem { Id = 2, Code = "6666666666" }
        );
        await _dbContext.SaveChangesAsync();
        var dto = new FeacnInsertItemDto { Id = 1, Code = "6666666666" };
        var result = await _controller.UpdateItem(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That((result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Delete_ReturnsNotFound_WhenItemMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.DeleteItem(10);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That((result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Delete_ReturnsForbidden_ForLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.DeleteItem(1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That((result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    private class ThrowingDbContext : AppDbContext
    {
        public bool ThrowOnSave { get; set; }
        public ThrowingDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
                throw new DbUpdateException("dup", new System.Exception("IX_insert_items_code"));
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [Test]
    public async Task Create_ReturnsConflict_OnDbConstraintException()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"throw_db_{System.Guid.NewGuid()}")
            .Options;
        var db = new ThrowingDbContext(options);

        var role = new Role { Id = 1, Name = "administrator", Title = "Администратор" };
        var user = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = "pwd",
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = role } ]
        };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.SaveChanges();

        var mockCtx = new Mock<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = 1;
        mockCtx.Setup(x => x.HttpContext).Returns(ctx);
        var logger = new LoggerFactory().CreateLogger<FeacnInsertItemsController>();
        var userSvc = new UserInformationService(db);
        var controller = new FeacnInsertItemsController(mockCtx.Object, db, userSvc, logger);

        db.ThrowOnSave = true;
        var dto = new FeacnInsertItemDto { Code = "7777777777" };
        var result = await controller.CreateItem(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That((result.Result as ObjectResult)!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }
}

