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
using Logibooks.Core.Services;
using System;
using System.Threading;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnCodesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IUpdateFeacnCodesService> _mockService;
    private ILogger<FeacnCodesController> _logger;
    private FeacnCodesController _controller;
    private Role _adminRole;
    private Role _userRole;
    private User _adminUser;
    private User _regularUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacn_controller_db_{System.Guid.NewGuid()}")
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
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }]
        };
        _regularUser = new User
        {
            Id = 2,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _userRole }]
        };
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockService = new Mock<IUpdateFeacnCodesService>();
        _logger = new LoggerFactory().CreateLogger<FeacnCodesController>();
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _mockService.Object, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (_dbContext != null)
        {
            try
            {
                _dbContext.Database.EnsureDeleted();
                _dbContext.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _mockService.Object, _logger);
    }

    [Test]
    public async Task Update_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var result = await _controller.Update();
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Update_ReturnsNoContent_ForAdmin()
    {
        SetCurrentUserId(1);
        var result = await _controller.Update();
        _mockService.Verify(s => s.UpdateAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }


    [Test]
    public async Task GetAllOrders_ReturnsOrders()
    {
        SetCurrentUserId(2);
        _dbContext.FeacnOrders.Add(new FeacnOrder { Id = 1, Title = "OrderTitle2", Url = "0100" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetAllOrders();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        var dto = result.Value!.First();
        Assert.That(dto.Title, Is.EqualTo("OrderTitle2"));
        Assert.That(dto.Url, Is.Not.Null);
    }

    [Test]
    public async Task GetPrefixes_ReturnsPrefixesForOrder()
    {
        SetCurrentUserId(2);
        var order = new FeacnOrder { Id = 1, Title = "OrderTitle3" };
        var prefix = new FeacnPrefix { Id = 2, Code = "12", FeacnOrderId = 1, FeacnOrder = order };
        var ex = new FeacnPrefixException { Id = 3, Code = "12a", FeacnPrefixId = 2, FeacnPrefix = prefix };
        _dbContext.FeacnOrders.Add(order);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.FeacnPrefixExceptions.Add(ex);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPrefixes(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Exceptions.Count, Is.EqualTo(1));
    }

}
