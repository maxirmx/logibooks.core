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


namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class StopWordsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<StopWordsController> _logger;
    private StopWordsController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"stop_words_controller_db_{System.Guid.NewGuid()}")
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
        _logger = new LoggerFactory().CreateLogger<StopWordsController>();
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetStopWords_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.StopWords.AddRange(new StopWord { Id = 1, Word = "a" }, new StopWord { Id = 2, Word = "b" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStopWords();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new StopWordDto { Word = "test", ExactMatch = false };
        var created = await _controller.PostStopWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as StopWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "updated";
        var upd = await _controller.PutStopWord(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteStopWord(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new StopWordDto { Word = "w" };
        var result = await _controller.PostStopWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteStopWord_AllowsCascadeDeletion_WhenUsed()
    {
        SetCurrentUserId(1);
        var word = new StopWord { Id = 5, Word = "used" };
        var reg = new Register { Id = 1, FileName = "r" };
        var order = new WbrOrder { Id = 1, RegisterId = 1 };
        var link = new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 5, BaseOrder = order, StopWord = word };
        _dbContext.StopWords.Add(word);
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(order);
        _dbContext.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteStopWord(5);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        // Verify the link is also deleted
        Assert.That(_dbContext.StopWords.Find(5), Is.Null);
        Assert.That(_dbContext.Set<BaseOrderStopWord>().Any(x => x.StopWordId == 5), Is.False);
    }

    [Test]
    public async Task GetStopWord_ReturnsWord_WhenExists()
    {
        SetCurrentUserId(2);
        var word = new StopWord { Id = 6, Word = "find" };
        _dbContext.StopWords.Add(word);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStopWord(6);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Word, Is.EqualTo("find"));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenWordAlreadyExists()
    {
        SetCurrentUserId(1); // Admin
        var existing = new StopWord { Id = 10, Word = "duplicate", ExactMatch = false };
        _dbContext.StopWords.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Word = "duplicate", ExactMatch = false };
        var result = await _controller.PostStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Edit_ReturnsConflict_WhenChangingToExistingWord()
    {
        SetCurrentUserId(1); // Admin
        var word1 = new StopWord { Id = 11, Word = "first", ExactMatch = false };
        var word2 = new StopWord { Id = 12, Word = "second", ExactMatch = false };
        _dbContext.StopWords.AddRange(word1, word2);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Id = 11, Word = "second", ExactMatch = false };
        var result = await _controller.PutStopWord(11, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Update_ReturnsForbidden_ForNonAdmin()
    {
        // Arrange: create a stop word as admin
        SetCurrentUserId(1);
        var word = new StopWord { Id = 100, Word = "editme", ExactMatch = false };
        _dbContext.StopWords.Add(word);
        await _dbContext.SaveChangesAsync();

        // Act: try to update as non-admin
        SetCurrentUserId(2);
        var dto = new StopWordDto { Id = 100, Word = "edited", ExactMatch = true };
        var result = await _controller.PutStopWord(100, dto);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Delete_ReturnsForbidden_ForNonAdmin()
    {
        // Arrange: create a stop word as admin
        SetCurrentUserId(1);
        var word = new StopWord { Id = 101, Word = "deleteme", ExactMatch = false };
        _dbContext.StopWords.Add(word);
        await _dbContext.SaveChangesAsync();

        // Act: try to delete as non-admin
        SetCurrentUserId(2);
        var result = await _controller.DeleteStopWord(101);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }
    [Test]
    public async Task GetStopWord_Returns404_WhenNotFound()
    {
        SetCurrentUserId(2); // Logist or any valid user
                             // Do not add any StopWord with Id = 999
        var result = await _controller.GetStopWord(999);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("999"));
    }
    [Test]
    public async Task PutStopWord_ReturnsBadRequest_WhenIdMismatch()
    {
        SetCurrentUserId(1); // Admin
        var dto = new StopWordDto { Id = 123, Word = "word", ExactMatch = false };
        var result = await _controller.PutStopWord(999, dto); // id != dto.Id

        Assert.That(result, Is.TypeOf<BadRequestResult>());
    }

    [Test]
    public async Task PutStopWord_Returns404_WhenWordNotFound()
    {
        SetCurrentUserId(1); // Admin
        var dto = new StopWordDto { Id = 999, Word = "missing", ExactMatch = false };
        var result = await _controller.PutStopWord(999, dto); // No such StopWord in DB

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("999"));
    }

    [Test]
    public async Task PutStopWord_Returns404_WhenStopWordNotFound()
    {
        SetCurrentUserId(1); // Admin user
        var dto = new StopWordDto { Id = 999, Word = "missing", ExactMatch = false };
        // Do not add a StopWord with Id = 999 to the database

        var result = await _controller.PutStopWord(999, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("999"));
    }
}
