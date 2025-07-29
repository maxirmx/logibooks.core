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
using System.Collections.Generic;


namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class StopWordsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IMorphologySearchService> _mockMorphologySearchService;
    private ILogger<StopWordsController> _logger;
    private IUserInformationService _userService;
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
        _mockMorphologySearchService = new Mock<IMorphologySearchService>();
        
        // Setup default behavior: return true for all words unless specifically overridden
        _mockMorphologySearchService.Setup(x => x.CheckWord(It.IsAny<string>()))
            .Returns(true);

        _logger = new LoggerFactory().CreateLogger<StopWordsController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockMorphologySearchService.Object);
    }

    private static bool IsEnglishWord(string word)
    {
        // Simple check: if all characters are Latin alphabet, consider it English
        return !string.IsNullOrEmpty(word) && word.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
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
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, _mockMorphologySearchService.Object);
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
        
        // Setup mock to allow this specific test word to pass morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("проверка")).Returns(true);
        _mockMorphologySearchService.Setup(x => x.CheckWord("обновление")).Returns(true);
        
        var dto = new StopWordDto { Word = "test", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var created = await _controller.PostStopWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as StopWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "обновление";
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
        var existing = new StopWord { Id = 10, Word = "повтор", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Word = "повтор", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PostStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Edit_ReturnsConflict_WhenChangingToExistingWord()
    {
        SetCurrentUserId(1); // Admin
        var word1 = new StopWord { Id = 11, Word = "первый", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var word2 = new StopWord { Id = 12, Word = "второй", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.AddRange(word1, word2);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Id = 11, Word = "второй", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
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
        var word = new StopWord { Id = 100, Word = "время", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(word);
        await _dbContext.SaveChangesAsync();

        // Act: try to update as non-admin
        SetCurrentUserId(2);
        var dto = new StopWordDto { Id = 100, Word = "временный", MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols };
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
        var word = new StopWord { Id = 101, Word = "удалить", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
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
        var dto = new StopWordDto { Id = 123, Word = "слово", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PutStopWord(999, dto); // id != dto.Id

        Assert.That(result, Is.TypeOf<BadRequestResult>());
    }

    [Test]
    public async Task PutStopWord_Returns404_WhenWordNotFound()
    {
        SetCurrentUserId(1); // Admin
        var dto = new StopWordDto { Id = 999, Word = "пропущенный", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
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
        var dto = new StopWordDto { Id = 999, Word = "пропущенный", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
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

    [Test]
    public async Task PostStopWord_Returns501_WhenMorphologyValidationFails()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return false for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("hello"))
            .Returns(false);
        
        var dto = new StopWordDto { Word = "hello", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PostStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status501NotImplemented));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("hello"));
        Assert.That(err.Msg, Does.Contain("отсутсвует в словаре"));
    }

    [Test]
    public async Task PostStopWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return true for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("тест"))
            .Returns(true);
        
        var dto = new StopWordDto { Word = "тест", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PostStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<StopWordDto>());
        var createdDto = created.Value as StopWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("тест"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)StopWordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task PostStopWord_SkipsMorphologyValidation_WhenExactMatchIsTrue()
    {
        SetCurrentUserId(1); // Admin user
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols
        var dto = new StopWordDto { Word = "hello", MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols };
        var result = await _controller.PostStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<StopWordDto>());
        var createdDto = created.Value as StopWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("hello"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)StopWordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("hello"), Times.Never);
    }

    [Test]
    public async Task PutStopWord_Returns501_WhenMorphologyValidationFails()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing stop word
        var existingWord = new StopWord { Id = 200, Word = "существующий", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existingWord);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return false for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("invalid"))
            .Returns(false);
        
        var dto = new StopWordDto { Id = 200, Word = "invalid", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PutStopWord(200, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status501NotImplemented));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("invalid"));
        Assert.That(err.Msg, Does.Contain("отсутсвует в словаре"));
    }

    [Test]
    public async Task PutStopWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing stop word
        var existingWord = new StopWord { Id = 201, Word = "старый", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existingWord);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return true for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("новый"))
            .Returns(true);
        
        var dto = new StopWordDto { Id = 201, Word = "новый", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        var result = await _controller.PutStopWord(201, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the word was updated
        var updatedWord = await _dbContext.StopWords.FindAsync(201);
        Assert.That(updatedWord!.Word, Is.EqualTo("новый"));
        Assert.That(updatedWord.MatchTypeId, Is.EqualTo((int)StopWordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task PutStopWord_SkipsMorphologyValidation_WhenExactMatchIsTrue()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing stop word
        var existingWord = new StopWord { Id = 202, Word = "старое", MatchTypeId = (int)StopWordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existingWord);
        await _dbContext.SaveChangesAsync();
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols
        var dto = new StopWordDto { Id = 202, Word = "badword", MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols };
        var result = await _controller.PutStopWord(202, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the word was updated
        var updatedWord = await _dbContext.StopWords.FindAsync(202);
        Assert.That(updatedWord!.Word, Is.EqualTo("badword"));
        Assert.That(updatedWord.MatchTypeId, Is.EqualTo((int)StopWordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("badword"), Times.Never);
    }

    [Test]
    public async Task GetMatchTypes_ReturnsAllMatchTypes()
    {
        // Arrange
        _dbContext.StopWordMatchTypes.Add(new StopWordMatchType { Id = 1, Name = "Type1" });
        _dbContext.StopWordMatchTypes.Add(new StopWordMatchType { Id = 2, Name = "Type2" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetMatchTypes();

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult!.Value, Is.InstanceOf<IEnumerable<StopWordMatchType>>());
        var matchTypes = okResult.Value as IEnumerable<StopWordMatchType>;
        Assert.That(matchTypes!.Count(), Is.EqualTo(2));
        Assert.That(matchTypes!.Any(mt => mt.Id == 1 && mt.Name == "Type1"));
        Assert.That(matchTypes!.Any(mt => mt.Id == 2 && mt.Name == "Type2"));
    }
}
