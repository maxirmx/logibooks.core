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
using System.IO;

using Moq;
using NUnit.Framework;
using ClosedXML.Excel;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using MorphologySupportLevel = Logibooks.Core.Interfaces.MorphologySupportLevel;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class KeyWordsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IMorphologySearchService> _mockMorphologySearchService;
    private ILogger<KeyWordsController> _logger;
    private IUserInformationService _userService;
    private KeyWordsController _controller;
    private IKeywordsProcessingService _keywordsProcessingService;
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
        _mockMorphologySearchService = new Mock<IMorphologySearchService>();
        
        // Setup default behavior: return FullSupport for all words unless specifically overridden
        _mockMorphologySearchService.Setup(x => x.CheckWord(It.IsAny<string>()))
            .Returns(MorphologySupportLevel.FullSupport);

        _logger = new LoggerFactory().CreateLogger<KeyWordsController>();
        _userService = new UserInformationService(_dbContext);
        var kwLogger = new LoggerFactory().CreateLogger<KeywordsProcessingService>();
        _keywordsProcessingService = new KeywordsProcessingService(_dbContext, kwLogger, _mockMorphologySearchService.Object);
        _controller = new KeyWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _keywordsProcessingService, _mockMorphologySearchService.Object, _logger);
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
        _controller = new KeyWordsController(_mockHttpContextAccessor.Object, _dbContext, _userService, _keywordsProcessingService, _mockMorphologySearchService.Object, _logger);
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
        
        // Setup mock to allow test words to pass morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("test")).Returns(MorphologySupportLevel.FullSupport);
        _mockMorphologySearchService.Setup(x => x.CheckWord("upd")).Returns(MorphologySupportLevel.FullSupport);

        var dto = new KeyWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["1234567890"] };
        var created = await _controller.CreateKeyWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as KeyWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "upd";
        createdDto.FeacnCodes = ["5678901234"];
        var upd = await _controller.UpdateKeyWord(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteKeyWord(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new KeyWordDto { Word = "w", FeacnCodes = [] }; // Empty FeacnCodes are now allowed
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Delete_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(1);
        var kw = new KeyWord { Id = 5, Word = "del" };
        kw.KeyWordFeacnCodes = []; // Empty collection is now allowed
        _dbContext.KeyWords.Add(kw);
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
        var existingKeyword = new KeyWord { Id = 10, Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 10, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        var dto = new KeyWordDto { Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenWordAndFeacnCodePairExists()
    {
        SetCurrentUserId(1);
        // Create a keyword with specific word and FeacnCode
        var existingKeyword = new KeyWord { Id = 10, Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 10, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Try to create the same (word, FeacnCode) pair - should conflict
        var dto = new KeyWordDto { Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenSameWordButDifferentFeacnCode()
    {
        SetCurrentUserId(1);
        // Create a keyword with specific word and FeacnCode
        var existingKeyword = new KeyWord { Id = 10, Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 10, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Try to create the same word with different FeacnCode - should now return conflict (changed behavior)
        var dto = new KeyWordDto { Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["0987654321"] };
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task GetKeyWord_ReturnsKeywordOrNotFound()
    {
        SetCurrentUserId(1);
        var kw = new KeyWord { Id = 100, Word = "findme", MatchTypeId = 1 };
        kw.KeyWordFeacnCodes = []; // Empty collection is allowed
        _dbContext.KeyWords.Add(kw);
        await _dbContext.SaveChangesAsync();

        var found = await _controller.GetKeyWord(100);
        Assert.That(found.Value, Is.Not.Null);
        Assert.That(found.Value!.Word, Is.EqualTo("findme"));

        var notFound = await _controller.GetKeyWord(999);
        Assert.That(notFound.Result, Is.TypeOf<ObjectResult>());
        var obj = notFound.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateKeyWord_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new KeyWordDto { Id = 1, Word = "w", MatchTypeId = 1, FeacnCodes = [] }; // Empty FeacnCodes are allowed
        var result = await _controller.UpdateKeyWord(1, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateKeyWord_ReturnsBadRequest_WhenIdMismatch()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Id = 2, Word = "w", MatchTypeId = 1, FeacnCodes = [] }; // Empty FeacnCodes are allowed
        var result = await _controller.UpdateKeyWord(1, dto);
        Assert.That(result, Is.TypeOf<BadRequestResult>());
    }

    [Test]
    public async Task UpdateKeyWord_ReturnsNotFound_WhenKeywordMissing()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Id = 999, Word = "w", MatchTypeId = 1, FeacnCodes = [] }; // Empty FeacnCodes are allowed
        var result = await _controller.UpdateKeyWord(999, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateKeyWord_ReturnsConflict_WhenDuplicateWord()
    {
        SetCurrentUserId(1);
        var kw1 = new KeyWord { Id = 1, Word = "dup", MatchTypeId = 1 };
        kw1.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 1, FeacnCode = "1234567890", KeyWord = kw1 }];
        var kw2 = new KeyWord { Id = 2, Word = "other", MatchTypeId = 1 };
        kw2.KeyWordFeacnCodes = []; // Empty collection for the second keyword
        _dbContext.KeyWords.AddRange(kw1, kw2);
        await _dbContext.SaveChangesAsync();
        var dto = new KeyWordDto { Id = 2, Word = "dup", MatchTypeId = 1, FeacnCodes = ["1234567890"] };
        var result = await _controller.UpdateKeyWord(2, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task DeleteKeyWord_ReturnsNotFound_WhenKeywordMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.DeleteKeyWord(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Upload_UpdatesKeywords_ForAdmin()
    {
        SetCurrentUserId(1);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");
        ws.Cell(1, 1).Value = "код";
        ws.Cell(1, 2).Value = "наименование";
        ws.Cell(2, 1).Value = "1234567890";
        ws.Cell(2, 2).Value = "товар";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        IFormFile file = new FormFile(ms, 0, ms.Length, "file", "kw.xlsx");

        var result = await _controller.Upload(file);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(_dbContext.KeyWords.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Upload_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");
        ws.Cell(1, 1).Value = "код";
        ws.Cell(1, 2).Value = "наименование";
        ws.Cell(2, 1).Value = "1234567890";
        ws.Cell(2, 2).Value = "товар";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        IFormFile file = new FormFile(ms, 0, ms.Length, "file", "kw.xlsx");

        var result = await _controller.Upload(file);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenServiceThrows()
    {
        SetCurrentUserId(1);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");
        ws.Cell(1, 1).Value = "код";
        ws.Cell(1, 2).Value = "наименование";
        ws.Cell(2, 1).Value = "123"; // invalid code
        ws.Cell(2, 2).Value = "товар";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        IFormFile file = new FormFile(ms, 0, ms.Length, "file", "bad.xlsx");

        var result = await _controller.Upload(file);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var msg = (obj.Value as ErrMessage)!.Msg;
        Assert.That(msg, Does.Contain("должен содержать ровно 10 цифр"));
    }


    [Test]
    public async Task CreateKeyWord_Returns418_WhenMorphologyValidationFails_NoSupport()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return NoSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("unsupported"))
            .Returns(MorphologySupportLevel.NoSupport);
        
        var dto = new KeyWordDto { Word = "unsupported", MatchTypeId = (int)WordMatchTypeCode.MorphologyMatchTypes, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status418ImATeapot));
        Assert.That(obj.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto = obj.Value as MorphologySupportLevelDto;
        Assert.That(mslDto!.Word, Is.EqualTo("unsupported"));
        Assert.That(mslDto.Level, Is.EqualTo((int)MorphologySupportLevel.NoSupport));
        Assert.That(mslDto.Msg, Does.Contain("отсутствует в словаре"));
    }

    [Test]
    public async Task CreateKeyWord_Returns418_WhenMorphologyValidationFails_FormsSupport()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return FormsSupport for morphology validation with StrongMorphology
        _mockMorphologySearchService.Setup(x => x.CheckWord("onlyforms"))
            .Returns(MorphologySupportLevel.FormsSupport);
        
        var dto = new KeyWordDto { Word = "onlyforms", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status418ImATeapot));
        Assert.That(obj.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto = obj.Value as MorphologySupportLevelDto;
        Assert.That(mslDto!.Word, Is.EqualTo("onlyforms"));
        Assert.That(mslDto.Level, Is.EqualTo((int)MorphologySupportLevel.FormsSupport));
        Assert.That(mslDto.Msg, Does.Contain("не поддерживается словарём"));
    }

    [Test]
    public async Task CreateKeyWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return FullSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("тест"))
            .Returns(MorphologySupportLevel.FullSupport);
        
        var dto = new KeyWordDto { Word = "тест", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<KeyWordDto>());
        var createdDto = created.Value as KeyWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("тест"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task CreateKeyWord_SkipsMorphologyValidation_WhenExactMatch()
    {
        SetCurrentUserId(1); // Admin user
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = ExactSymbols
        var dto = new KeyWordDto { Word = "hello", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<KeyWordDto>());
        var createdDto = created.Value as KeyWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("hello"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("hello"), Times.Never);
    }

    [Test]
    public async Task CreateKeyWord_AllowsFormsSupport_WhenWeakMorphology()
    {
        SetCurrentUserId(1); // Admin user
        
        // Setup mock to return FormsSupport - this should be allowed for WeakMorphology
        _mockMorphologySearchService.Setup(x => x.CheckWord("формы"))
            .Returns(MorphologySupportLevel.FormsSupport);
        
        var dto = new KeyWordDto { Word = "формы", MatchTypeId = (int)WordMatchTypeCode.WeakMorphology, FeacnCodes = ["1234567890"] };
        var result = await _controller.CreateKeyWord(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created!.Value, Is.InstanceOf<KeyWordDto>());
        var createdDto = created.Value as KeyWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("формы"));
        Assert.That(createdDto.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));
    }

    [Test]
    public async Task UpdateKeyWord_Returns418_WhenMorphologyValidationFails_NoSupport()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing keyword
        var existingKeyword = new KeyWord { Id = 300, Word = "old", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 300, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return NoSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("unsupported"))
            .Returns(MorphologySupportLevel.NoSupport);
        
        var dto = new KeyWordDto { Id = 300, Word = "unsupported", MatchTypeId = (int)WordMatchTypeCode.MorphologyMatchTypes, FeacnCodes = ["1234567890"] };
        var result = await _controller.UpdateKeyWord(300, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status418ImATeapot));
        Assert.That(obj.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto = obj.Value as MorphologySupportLevelDto;
        Assert.That(mslDto!.Word, Is.EqualTo("unsupported"));
        Assert.That(mslDto.Level, Is.EqualTo((int)MorphologySupportLevel.NoSupport));
        Assert.That(mslDto.Msg, Does.Contain("отсутствует в словаре"));
    }

    [Test]
    public async Task UpdateKeyWord_Returns418_WhenMorphologyValidationFails_FormsSupport()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing keyword
        var existingKeyword = new KeyWord { Id = 301, Word = "old", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 301, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return FormsSupport for morphology validation with StrongMorphology
        _mockMorphologySearchService.Setup(x => x.CheckWord("onlyforms"))
            .Returns(MorphologySupportLevel.FormsSupport);
        
        var dto = new KeyWordDto { Id = 301, Word = "onlyforms", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology, FeacnCodes = ["1234567890"] };
        var result = await _controller.UpdateKeyWord(301, dto);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status418ImATeapot));
        Assert.That(obj.Value, Is.InstanceOf<MorphologySupportLevelDto>());
        var mslDto = obj.Value as MorphologySupportLevelDto;
        Assert.That(mslDto!.Word, Is.EqualTo("onlyforms"));
        Assert.That(mslDto.Level, Is.EqualTo((int)MorphologySupportLevel.FormsSupport));
        Assert.That(mslDto.Msg, Does.Contain("не поддерживается словарём"));
    }

    [Test]
    public async Task UpdateKeyWord_SucceedsWithMorphologyValidation_WhenValidationPasses()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing keyword
        var existingKeyword = new KeyWord { Id = 302, Word = "старый", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 302, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Setup mock to return FullSupport for morphology validation
        _mockMorphologySearchService.Setup(x => x.CheckWord("новый"))
            .Returns(MorphologySupportLevel.FullSupport);
        
        var dto = new KeyWordDto { Id = 302, Word = "новый", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology, FeacnCodes = ["1234567890"] };
        var result = await _controller.UpdateKeyWord(302, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the keyword was updated
        var updatedKeyword = await _dbContext.KeyWords.FindAsync(302);
        Assert.That(updatedKeyword!.Word, Is.EqualTo("новый"));
        Assert.That(updatedKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.StrongMorphology));
    }

    [Test]
    public async Task UpdateKeyWord_SkipsMorphologyValidation_WhenExactMatch()
    {
        SetCurrentUserId(1); // Admin user
        
        // Create an existing keyword
        var existingKeyword = new KeyWord { Id = 303, Word = "старое", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 303, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Even though this would fail morphology validation, it should succeed because MatchTypeId = ExactSymbols
        var dto = new KeyWordDto { Id = 303, Word = "badword", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["1234567890"] };
        var result = await _controller.UpdateKeyWord(303, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        // Verify the keyword was updated
        var updatedKeyword = await _dbContext.KeyWords.FindAsync(303);
        Assert.That(updatedKeyword!.Word, Is.EqualTo("badword"));
        Assert.That(updatedKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.ExactSymbols));

        // Verify that CheckWord was never called for exact match
        _mockMorphologySearchService.Verify(x => x.CheckWord("badword"), Times.Never);
    }

    [Test]
    public async Task CreateKeyWord_SkipsMorphologyValidation_ForNonMorphologyMatchTypes()
    {
        SetCurrentUserId(1); // Admin user
        
        // Test with ExactWord - should skip morphology validation
        var dto1 = new KeyWordDto { Word = "exact", MatchTypeId = (int)WordMatchTypeCode.ExactWord, FeacnCodes = ["1234567890"] };
        var result1 = await _controller.CreateKeyWord(dto1);
        Assert.That(result1.Result, Is.TypeOf<CreatedAtActionResult>());

        // Test with Phrase - should skip morphology validation
        var dto2 = new KeyWordDto { Word = "phrase test", MatchTypeId = (int)WordMatchTypeCode.Phrase, FeacnCodes = ["0987654321"] };
        var result2 = await _controller.CreateKeyWord(dto2);
        Assert.That(result2.Result, Is.TypeOf<CreatedAtActionResult>());

        // Verify that CheckWord was never called for non-morphology match types
        _mockMorphologySearchService.Verify(x => x.CheckWord("exact"), Times.Never);
        _mockMorphologySearchService.Verify(x => x.CheckWord("phrase test"), Times.Never);
    }

    [Test]
    public async Task Create_AllowsEmptyFeacnCodes()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = [] };
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result.Result as CreatedAtActionResult;
        var createdDto = createdResult!.Value as KeyWordDto;
        Assert.That(createdDto!.Word, Is.EqualTo("test"));
        Assert.That(createdDto.FeacnCodes.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenInvalidFeacnCode()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["123"] }; // Invalid: only 3 digits
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That((obj.Value as ErrMessage)!.Msg, Does.Contain("Код ТН ВЭД должен состоять из 10 цифр"));
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenFeacnCodeContainsNonDigits()
    {
        SetCurrentUserId(1);
        var dto = new KeyWordDto { Word = "test", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = ["12345ABCDE"] }; // Invalid: contains letters
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That((obj.Value as ErrMessage)!.Msg, Does.Contain("Код ТН ВЭД должен состоять из 10 цифр"));
    }

    [Test]
    public async Task Create_ReturnsConflict_WhenSameWordWithEmptyFeacnCodes()
    {
        SetCurrentUserId(1);
        // Create a keyword with specific word and FeacnCode
        var existingKeyword = new KeyWord { Id = 10, Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        existingKeyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 10, FeacnCode = "1234567890", KeyWord = existingKeyword }];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();
        
        // Try to create the same word with empty FeacnCodes - should now return conflict (changed behavior)
        var dto = new KeyWordDto { Word = "dup", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols, FeacnCodes = [] };
        var result = await _controller.CreateKeyWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }
}
