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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using ClosedXML.Excel;
using NUnit.Framework;
using Moq;

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Services;

public class KeywordsProcessingServiceTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private KeywordsProcessingService _service;
    private ILogger<KeywordsProcessingService> _logger;
    private Mock<IMorphologySearchService> _mockMorphologySearchService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"kw_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        
        // Add word match types
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = (int)WordMatchTypeCode.ExactSymbols, Name = "Exact Symbols" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = (int)WordMatchTypeCode.ExactWord, Name = "Exact Word" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = (int)WordMatchTypeCode.Phrase, Name = "Phrase" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = (int)WordMatchTypeCode.WeakMorphology, Name = "Weak Morphology" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = (int)WordMatchTypeCode.StrongMorphology, Name = "Strong Morphology" });
        _dbContext.SaveChanges();

        _logger = new LoggerFactory().CreateLogger<KeywordsProcessingService>();
        _mockMorphologySearchService = new Mock<IMorphologySearchService>();
        
        // Setup default behavior: return FullSupport for all words unless specifically overridden
        _mockMorphologySearchService.Setup(x => x.CheckWord(It.IsAny<string>()))
            .Returns(MorphologySupportLevel.FullSupport);
            
        _service = new KeywordsProcessingService(_dbContext, _logger, _mockMorphologySearchService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private byte[] CreateTestExcelFile(List<(string code, string name)> rows, bool includeHeader = true)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");

        int rowIndex = 1;

        if (includeHeader)
        {
            worksheet.Cell(rowIndex, 1).Value = "Код";
            worksheet.Cell(rowIndex, 2).Value = "Наименование";
            rowIndex++;
        }

        foreach (var (code, name) in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = code;
            worksheet.Cell(rowIndex, 2).Value = name;
            rowIndex++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private byte[] CreateEmptyExcelFile()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sheet1");
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_ValidFile_ShouldProcessKeywords()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "слово"),
            ("0987654321", "другое, фраза с пробелами")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(3)); // 3 keywords total (1 from first row, 2 from second row)

        var savedKeywords = await _dbContext.KeyWords.ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(3));

        // Verify first keyword
        var keyword1 = savedKeywords.FirstOrDefault(k => k.Word == "слово");
        Assert.That(keyword1, Is.Not.Null);
        Assert.That(keyword1!.FeacnCode, Is.EqualTo("1234567890"));
        Assert.That(keyword1.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        // Verify second keyword (single word)
        var keyword2 = savedKeywords.FirstOrDefault(k => k.Word == "другое");
        Assert.That(keyword2, Is.Not.Null);
        Assert.That(keyword2!.FeacnCode, Is.EqualTo("0987654321"));
        Assert.That(keyword2.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        // Verify third keyword (phrase)
        var keyword3 = savedKeywords.FirstOrDefault(k => k.Word == "фраза с пробелами");
        Assert.That(keyword3, Is.Not.Null);
        Assert.That(keyword3!.FeacnCode, Is.EqualTo("0987654321"));
        Assert.That(keyword3.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_NineDigitCode_ShouldPrependZero()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("123456789", "тестовое слово")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].FeacnCode, Is.EqualTo("0123456789")); // Zero prepended

        var savedKeyword = await _dbContext.KeyWords.FirstOrDefaultAsync();
        Assert.That(savedKeyword, Is.Not.Null);
        Assert.That(savedKeyword!.FeacnCode, Is.EqualTo("0123456789"));
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_InvalidCodeLength_ShouldThrowException()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("12345678", "тестовое слово") // 8 digits, not 9 or 10
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx"));
        
        Assert.That(ex!.Message, Does.Contain("должен содержать ровно 10 цифр"));
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_EmptyFile_ShouldThrowException()
    {
        // Arrange
        var excelBytes = CreateEmptyExcelFile();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx"));
        
        Assert.That(ex!.Message, Is.EqualTo("Файл не содержит данных"));
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_MissingColumns_ShouldThrowException()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell(1, 1).Value = "Не код";
        worksheet.Cell(1, 2).Value = "Не наименование";
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var excelBytes = ms.ToArray();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx"));
        
        Assert.That(ex!.Message, Is.EqualTo("Не найдены столбцы 'код' и 'наименование'"));
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_DuplicateWords_ShouldThrowException()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "тестовое слово"),
            ("0987654321", "тестовое слово") // Duplicate word with different code
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx"));
        
        Assert.That(ex!.Message, Does.Contain("Ключевые слова и фразы заданы более одного раза"));
        Assert.That(ex!.Message, Does.Contain("тестовое слово"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_UpdatesExistingKeywords()
    {
        // Arrange - Add existing keyword
        var existingKeyword = new KeyWord
        {
            Word = "тестовое слово",
            FeacnCode = "1111111111",
            MatchTypeId = (int)WordMatchTypeCode.ExactSymbols
        };
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();

        // Create Excel with updated code for the same word
        var testData = new List<(string, string)>
        {
            ("2222222222", "тестовое слово")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));

        var updatedKeyword = await _dbContext.KeyWords.FirstOrDefaultAsync(k => k.Word == "тестовое слово");
        Assert.That(updatedKeyword, Is.Not.Null);
        Assert.That(updatedKeyword!.FeacnCode, Is.EqualTo("2222222222")); // Code updated
        Assert.That(updatedKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase)); // Since "тестовое слово" contains a space, it's a phrase
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_RemovesUnlistedKeywords()
    {
        // Arrange - Add existing keywords
        var existingKeywords = new List<KeyWord>
        {
            new() { Word = "слово1", FeacnCode = "1111111111", MatchTypeId = (int)WordMatchTypeCode.ExactWord },
            new() { Word = "слово2", FeacnCode = "2222222222", MatchTypeId = (int)WordMatchTypeCode.ExactWord },
            new() { Word = "слово3", FeacnCode = "3333333333", MatchTypeId = (int)WordMatchTypeCode.ExactWord }
        };
        _dbContext.KeyWords.AddRange(existingKeywords);
        await _dbContext.SaveChangesAsync();

        // Create Excel with only one of the existing words
        var testData = new List<(string, string)>
        {
            ("1111111111", "слово1")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));

        var remainingKeywords = await _dbContext.KeyWords.ToListAsync();
        Assert.That(remainingKeywords.Count, Is.EqualTo(1));
        Assert.That(remainingKeywords[0].Word, Is.EqualTo("слово1"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_TrimsWhitespace()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "  слово с пробелами   ")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Word, Is.EqualTo("слово с пробелами")); // Trimmed

        var savedKeyword = await _dbContext.KeyWords.FirstOrDefaultAsync();
        Assert.That(savedKeyword, Is.Not.Null);
        Assert.That(savedKeyword!.Word, Is.EqualTo("слово с пробелами"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_SkipsEmptyRowsAndColumns()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        
        // Header
        worksheet.Cell(1, 1).Value = "Код";
        worksheet.Cell(1, 2).Value = "Наименование";
        
        // Row with data
        worksheet.Cell(2, 1).Value = "1234567890";
        worksheet.Cell(2, 2).Value = "тестовое слово";
        
        // Empty row
        worksheet.Cell(3, 1).Value = ""; 
        worksheet.Cell(3, 2).Value = "";
        
        // Row with empty code
        worksheet.Cell(4, 1).Value = "";
        worksheet.Cell(4, 2).Value = "слово без кода";
        
        // Row with empty name
        worksheet.Cell(5, 1).Value = "9876543210";
        worksheet.Cell(5, 2).Value = "";
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var excelBytes = ms.ToArray();

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1)); // Only one valid row
        Assert.That(result[0].Word, Is.EqualTo("тестовое слово"));

        var savedKeywords = await _dbContext.KeyWords.ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_HandlesMultipleWordsPerCell()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "слово1, слово2, фраза с пробелами, ещё одна фраза")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(4)); // 4 keywords from one cell

        var savedKeywords = await _dbContext.KeyWords.ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(4));
        
        // Check each word was processed correctly
        Assert.That(savedKeywords.Any(k => k.Word == "слово1" && k.MatchTypeId == (int)WordMatchTypeCode.WeakMorphology));
        Assert.That(savedKeywords.Any(k => k.Word == "слово2" && k.MatchTypeId == (int)WordMatchTypeCode.WeakMorphology));
        Assert.That(savedKeywords.Any(k => k.Word == "фраза с пробелами" && k.MatchTypeId == (int)WordMatchTypeCode.Phrase));
        Assert.That(savedKeywords.Any(k => k.Word == "ещё одна фраза" && k.MatchTypeId == (int)WordMatchTypeCode.Phrase));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_CorrectlyDetectsMatchType()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "однослово"),
            ("0987654321", "фраза с пробелами")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));

        // Verify match types
        var singleWord = result.FirstOrDefault(k => k.Word == "однослово");
        Assert.That(singleWord, Is.Not.Null);
        Assert.That(singleWord!.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        var phrase = result.FirstOrDefault(k => k.Word == "фраза с пробелами");
        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_SquashesDuplicateWordsInLine()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "слово, слово, СЛОВО") // Same word repeated with different case
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1)); // Only one unique word after case-insensitive deduplication
        Assert.That(result[0].Word, Is.EqualTo("слово"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_ProcessesCyrillicCorrectly()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "русское слово"),
            ("0987654321", "КИРИЛЛИЧЕСКИЕ СИМВОЛЫ")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        
        // Check case conversion (to lowercase in Russian culture)
        Assert.That(result.Any(k => k.Word == "русское слово"));
        Assert.That(result.Any(k => k.Word == "кириллические символы")); // Converted to lowercase
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_InvalidExcelFormat_ShouldThrowException()
    {
        // Arrange - create an invalid file (not Excel)
        var invalidBytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _service.UploadKeywordsFromExcelAsync(invalidBytes, "invalid.xlsx"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_UsesMorphologyCheck_ForSingleWords()
    {
        // Arrange
        _mockMorphologySearchService.Setup(x => x.CheckWord("поддерживаемое"))
            .Returns(MorphologySupportLevel.FullSupport);
        _mockMorphologySearchService.Setup(x => x.CheckWord("неподдерживаемое"))
            .Returns(MorphologySupportLevel.NoSupport);
            
        var testData = new List<(string, string)>
        {
            ("1234567890", "поддерживаемое"),
            ("0987654321", "неподдерживаемое")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));

        // Verify morphology-supported word gets WeakMorphology
        var supportedWord = result.FirstOrDefault(k => k.Word == "поддерживаемое");
        Assert.That(supportedWord, Is.Not.Null);
        Assert.That(supportedWord!.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        // Verify non-supported word gets ExactSymbols
        var unsupportedWord = result.FirstOrDefault(k => k.Word == "неподдерживаемое");
        Assert.That(unsupportedWord, Is.Not.Null);
        Assert.That(unsupportedWord!.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.ExactSymbols));

        // Verify CheckWord was called for both single words
        _mockMorphologySearchService.Verify(x => x.CheckWord("поддерживаемое"), Times.Once);
        _mockMorphologySearchService.Verify(x => x.CheckWord("неподдерживаемое"), Times.Once);
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_SkipsMorphologyCheck_ForPhrases()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "фраза с пробелами")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1));

        // Verify phrase gets Phrase match type
        var phrase = result.FirstOrDefault(k => k.Word == "фраза с пробелами");
        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase));

        // Verify CheckWord was never called for phrases
        _mockMorphologySearchService.Verify(x => x.CheckWord("фраза с пробелами"), Times.Never);
    }
}

