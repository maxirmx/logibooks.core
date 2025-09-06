// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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
using MorphologySupportLevel = Logibooks.Core.Interfaces.MorphologySupportLevel;
using Logibooks.Core.Interfaces;

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
        
        // Setup default behavior: return FullSupport for all words unless specifically overridden
        _mockMorphologySearchService.Setup(x => x.CheckWord(It.IsAny<string>()))
            .Returns(MorphologySupportLevel.FullSupport);

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
        _mockMorphologySearchService.Setup(x => x.CheckWord("проверка")).Returns(MorphologySupportLevel.FullSupport);
        _mockMorphologySearchService.Setup(x => x.CheckWord("обновление")).Returns(MorphologySupportLevel.FullSupport);
        
        var dto = new StopWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var created = await _controller.CreateStopWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as StopWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "обновление";
        var upd = await _controller.UpdateStopWord(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteStopWord(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new StopWordDto { Word = "w" };
        var result = await _controller.CreateStopWord(dto);
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
        var order = new WbrParcel { Id = 1, RegisterId = 1 };
        var link = new BaseParcelStopWord { BaseParcelId = 1, StopWordId = 5, BaseParcel = order, StopWord = word };
        _dbContext.StopWords.Add(word);
        _dbContext.Registers.Add(reg);
        _dbContext.Parcels.Add(order);
        _dbContext.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteStopWord(5);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        // Verify the link is also deleted
        Assert.That(_dbContext.StopWords.Find(5), Is.Null);
        Assert.That(_dbContext.Set<BaseParcelStopWord>().Any(x => x.StopWordId == 5), Is.False);
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
        var existing = new StopWord { Id = 10, Word = "повтор", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existing);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Word = "повтор", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.CreateStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Edit_ReturnsConflict_WhenChangingToExistingWord()
    {
        SetCurrentUserId(1); // Admin
        var word1 = new StopWord { Id = 11, Word = "первый", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var word2 = new StopWord { Id = 12, Word = "второй", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.AddRange(word1, word2);
        await _dbContext.SaveChangesAsync();

        var dto = new StopWordDto { Id = 11, Word = "второй", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.UpdateStopWord(11, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Update_ReturnsForbidden_ForNonAdmin()
    {
        // Arrange: create a stop word as admin
        SetCurrentUserId(1);
        var word = new StopWord { Id = 100, Word = "время", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(word);
        await _dbContext.SaveChangesAsync();

        // Act: try to update as non-admin
        SetCurrentUserId(2);
        var dto = new StopWordDto { Id = 100, Word = "временный", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var result = await _controller.UpdateStopWord(100, dto);

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
        var word = new StopWord { Id = 101, Word = "удалить", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
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
    public async Task UpdateStopWord_ReturnsBadRequest_WhenIdMismatch()
    {
        SetCurrentUserId(1); // Admin
        var dto = new StopWordDto { Id = 123, Word = "слово", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.UpdateStopWord(999, dto); // id != dto.Id

        Assert.That(result, Is.TypeOf<BadRequestResult>());
    }

    [Test]
    public async Task UpdateStopWord_Returns404_WhenWordNotFound()
    {
        SetCurrentUserId(1); // Admin
        var dto = new StopWordDto { Id = 999, Word = "пропущенный", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.UpdateStopWord(999, dto); // No such StopWord in DB

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("999"));
    }

    [Test]
    public async Task UpdateStopWord_Returns404_WhenStopWordNotFound()
    {
        SetCurrentUserId(1); // Admin user
        var dto = new StopWordDto { Id = 999, Word = "пропущенный", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        // Do not add a StopWord with Id = 999 to the database

        var result = await _controller.UpdateStopWord(999, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        Assert.That(obj.Value, Is.InstanceOf<ErrMessage>());
        var err = obj.Value as ErrMessage;
        Assert.That(err!.Msg, Does.Contain("999"));
    }

    [Test]
    public async Task PostStopWord_Returns418_WhenMorphologySupportLevelIsNoSupportOrFormsSupport()
    {
        SetCurrentUserId(1); // Admin user
        // MorphologyMatchTypes + NoSupport
        _mockMorphologySearchService.Setup(x => x.CheckWord("noform")).Returns(MorphologySupportLevel.NoSupport);
        var dto1 = new StopWordDto { Word = "noform", MatchTypeId = (int)WordMatchTypeCode.MorphologyMatchTypes };
        var result1 = await _controller.CreateStopWord(dto1);
        Assert.That(result1.Result, Is.TypeOf<ObjectResult>());
        var obj1 = result1.Result as ObjectResult;
        Assert.That(obj1!.StatusCode, Is.EqualTo(418));
        Assert.That(obj1.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto1 = obj1.Value as MorphologySupportLevelDto;
        Assert.That(mslDto1!.Word, Is.EqualTo("noform"));
        Assert.That(mslDto1.Level, Is.EqualTo((int)MorphologySupportLevel.NoSupport));
        Assert.That(mslDto1.Msg, Does.Contain("отсутствует в словаре"));

        // StrongMorphology + FormsSupport
        _mockMorphologySearchService.Setup(x => x.CheckWord("onlyforms")).Returns(MorphologySupportLevel.FormsSupport);
        var dto2 = new StopWordDto { Word = "onlyforms", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result2 = await _controller.CreateStopWord(dto2);
        Assert.That(result2.Result, Is.TypeOf<ObjectResult>());
        var obj2 = result2.Result as ObjectResult;
        Assert.That(obj2!.StatusCode, Is.EqualTo(418));
        Assert.That(obj2.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto2 = obj2.Value as MorphologySupportLevelDto;
        Assert.That(mslDto2!.Word, Is.EqualTo("onlyforms"));
        Assert.That(mslDto2.Level, Is.EqualTo((int)MorphologySupportLevel.FormsSupport));
        Assert.That(mslDto2.Msg, Does.Contain("не поддерживается словарём"));
    }

    [Test]
    public async Task PostStopWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return FullSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("тест"))
            .Returns(MorphologySupportLevel.FullSupport);
        
        var dto = new StopWordDto { Word = "тест", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.CreateStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<StopWordDto>());
        var createdDto = created.Value as StopWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("тест"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task PostStopWord_SkipsMorphologyValidation_WhenExactMatchIsTrue()
    {
        SetCurrentUserId(1); // Admin user
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols
        var dto = new StopWordDto { Word = "hello", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var result = await _controller.CreateStopWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<StopWordDto>());
        var createdDto = created.Value as StopWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("hello"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("hello"), Times.Never);
    }

    [Test]
    public async Task UpdateStopWord_Returns418_WhenMorphologySupportLevelIsNoSupportOrFormsSupport()
    {
        SetCurrentUserId(1); // Admin user
        var sw = new StopWord { Id = 300, Word = "old", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(sw);
        await _dbContext.SaveChangesAsync();

        // MorphologyMatchTypes + NoSupport
        _mockMorphologySearchService.Setup(x => x.CheckWord("noform")).Returns(MorphologySupportLevel.NoSupport);
        var dto1 = new StopWordDto { Id = 300, Word = "noform", MatchTypeId = (int)WordMatchTypeCode.MorphologyMatchTypes };
        var result1 = await _controller.UpdateStopWord(300, dto1);
        Assert.That(result1, Is.TypeOf<ObjectResult>());
        var obj1 = result1 as ObjectResult;
        Assert.That(obj1!.StatusCode, Is.EqualTo(418));
        Assert.That(obj1.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto1 = obj1.Value as MorphologySupportLevelDto;
        Assert.That(mslDto1!.Word, Is.EqualTo("noform"));
        Assert.That(mslDto1.Level, Is.EqualTo((int)MorphologySupportLevel.NoSupport));
        Assert.That(mslDto1.Msg, Does.Contain("отсутствует в словаре"));

        // StrongMorphology + FormsSupport
        _mockMorphologySearchService.Setup(x => x.CheckWord("onlyforms")).Returns(MorphologySupportLevel.FormsSupport);
        var dto2 = new StopWordDto { Id = 300, Word = "onlyforms", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result2 = await _controller.UpdateStopWord(300, dto2);
        Assert.That(result2, Is.TypeOf<ObjectResult>());
        var obj2 = result2 as ObjectResult;
        Assert.That(obj2!.StatusCode, Is.EqualTo(418));
        Assert.That(obj2.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto2 = obj2.Value as MorphologySupportLevelDto;
        Assert.That(mslDto2!.Word, Is.EqualTo("onlyforms"));
        Assert.That(mslDto2.Level, Is.EqualTo((int)MorphologySupportLevel.FormsSupport));
        Assert.That(mslDto2.Msg, Does.Contain("не поддерживается словарём"));
    }

    [Test]
    public async Task UpdateStopWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing stop word
        var existingWord = new StopWord { Id = 201, Word = "старый", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existingWord);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return FullSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("новый")).Returns(MorphologySupportLevel.FullSupport);
        
        var dto = new StopWordDto { Id = 201, Word = "новый", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        var result = await _controller.UpdateStopWord(201, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the word was updated
        var updatedWord = await _dbContext.StopWords.FindAsync(201);
        Assert.That(updatedWord!.Word, Is.EqualTo("новый"));
        Assert.That(updatedWord.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task UpdateStopWord_SkipsMorphologyValidation_WhenExactMatchIsTrue()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing stop word
        var existingWord = new StopWord { Id = 202, Word = "старое", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        _dbContext.StopWords.Add(existingWord);
        await _dbContext.SaveChangesAsync();
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = (int)StopWordMatchTypeCode.ExactSymbols
        var dto = new StopWordDto { Id = 202, Word = "badword", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var result = await _controller.UpdateStopWord(202, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the word was updated
        var updatedWord = await _dbContext.StopWords.FindAsync(202);
        Assert.That(updatedWord!.Word, Is.EqualTo("badword"));
        Assert.That(updatedWord.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("badword"), Times.Never);
    }
}
