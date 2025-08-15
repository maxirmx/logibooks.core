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

using System.Linq;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class KeyWordsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<KeyWordsController> _logger;
    private IUserInformationService _userService;
    private KeyWordsController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"key_words_controller_db_{System.Guid.NewGuid()}")
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
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }]
        };
        _logistUser = new User
        {
            Id = 2,
            Email = "logist@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _logistRole }]
        };
        _dbContext.Users.AddRange(_adminUser, _logistUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<KeyWordsController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new KeyWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
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
        _controller = new KeyWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
    }

    [Test]
    public async Task GetKeyWords_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.KeyWords.AddRange(new KeyWord { Id = 1, Word = "a" }, new KeyWord { Id = 2, Word = "b" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetKeyWords();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCode = "1234" };
        var created = await _controller.PostKeyWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as KeyWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "upd";
        createdDto.FeacnCode = "5678";
        var upd = await _controller.PutKeyWord(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteKeyWord(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new KeyWordDto { Word = "w" };
        var result = await _controller.PostKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Delete_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(1);
        _dbContext.KeyWords.Add(new KeyWord { Id = 5, Word = "del" });
        await _dbContext.SaveChangesAsync();
        SetCurrentUserId(2);
        var result = await _controller.DeleteKeyWord(5);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenWordExists()
    {
        SetCurrentUserId(1);
        _dbContext.KeyWords.Add(new KeyWord { Id = 10, Word = "dup" });
        await _dbContext.SaveChangesAsync();
        var dto = new KeyWordDto { Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var result = await _controller.PostKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }
}
