// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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

    private byte[] CreateTestExcelFileWithInsertColumns(List<(string code, string name, string? insertBefore, string? insertAfter)> rows, bool includeHeader = true)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");

        int rowIndex = 1;

        if (includeHeader)
        {
            worksheet.Cell(rowIndex, 1).Value = "Код";
            worksheet.Cell(rowIndex, 2).Value = "Наименование";
            worksheet.Cell(rowIndex, 3).Value = "Перед описанием";
            worksheet.Cell(rowIndex, 4).Value = "В конце описания";
            rowIndex++;
        }

        foreach (var (code, name, insertBefore, insertAfter) in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = code;
            worksheet.Cell(rowIndex, 2).Value = name;
            worksheet.Cell(rowIndex, 3).Value = insertBefore ?? "";
            worksheet.Cell(rowIndex, 4).Value = insertAfter ?? "";
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

        var savedKeywords = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(3));

        // Verify first keyword
        var keyword1 = savedKeywords.FirstOrDefault(k => k.Word == "слово");
        Assert.That(keyword1, Is.Not.Null);
        Assert.That(keyword1!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("1234567890"));
        Assert.That(keyword1.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        // Verify second keyword (single word)
        var keyword2 = savedKeywords.FirstOrDefault(k => k.Word == "другое");
        Assert.That(keyword2, Is.Not.Null);
        Assert.That(keyword2!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("0987654321"));
        Assert.That(keyword2.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        // Verify third keyword (phrase)
        var keyword3 = savedKeywords.FirstOrDefault(k => k.Word == "фраза с пробелами");
        Assert.That(keyword3, Is.Not.Null);
        Assert.That(keyword3!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("0987654321"));
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
        Assert.That(result[0].KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("0123456789")); // Zero prepended

        var savedKeyword = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .FirstOrDefaultAsync();
        Assert.That(savedKeyword, Is.Not.Null);
        Assert.That(savedKeyword!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("0123456789"));
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
    public async Task UploadKeywordsFromExcelAsync_UpdatesExistingKeywords()
    {
        // Arrange - Add existing keyword with existing FEACN codes
        var existingKeyword = new KeyWord
        {
            Word = "тестовое слово",
            MatchTypeId = (int)WordMatchTypeCode.ExactSymbols
        };
        existingKeyword.KeyWordFeacnCodes = [
            new KeyWordFeacnCode
            {
                FeacnCode = "1111111111",
                KeyWord = existingKeyword
            },
            new KeyWordFeacnCode
            {
                FeacnCode = "3333333333",
                KeyWord = existingKeyword
            }
        ];
        _dbContext.KeyWords.Add(existingKeyword);
        await _dbContext.SaveChangesAsync();

        // Create Excel with new code for the same word
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

        var updatedKeyword = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .FirstOrDefaultAsync(k => k.Word == "тестовое слово");
        Assert.That(updatedKeyword, Is.Not.Null);
        
        // Verify all FEACN codes are preserved (existing ones + new one)
        Assert.That(updatedKeyword!.KeyWordFeacnCodes.Count, Is.EqualTo(3));
        var feacnCodes = updatedKeyword.KeyWordFeacnCodes.Select(fc => fc.FeacnCode).ToList();
        Assert.That(feacnCodes, Contains.Item("1111111111")); // Original preserved
        Assert.That(feacnCodes, Contains.Item("2222222222")); // New one added
        Assert.That(feacnCodes, Contains.Item("3333333333")); // Original preserved
        
        // Verify match type is updated based on current word analysis
        Assert.That(updatedKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase)); // Since "тестовое слово" contains a space, it's a phrase
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_PreservesUnlistedKeywords()
    {
        // Arrange - Add existing keywords
        var existingKeywords = new List<KeyWord>();
        
        var kw1 = new KeyWord { Word = "слово1", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        kw1.KeyWordFeacnCodes = [new KeyWordFeacnCode { FeacnCode = "1111111111", KeyWord = kw1 }];
        existingKeywords.Add(kw1);
        
        var kw2 = new KeyWord { Word = "слово2", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        kw2.KeyWordFeacnCodes = [new KeyWordFeacnCode { FeacnCode = "2222222222", KeyWord = kw2 }];
        existingKeywords.Add(kw2);
        
        var kw3 = new KeyWord { Word = "слово3", MatchTypeId = (int)WordMatchTypeCode.ExactWord };
        kw3.KeyWordFeacnCodes = [new KeyWordFeacnCode { FeacnCode = "3333333333", KeyWord = kw3 }];
        existingKeywords.Add(kw3);
        
        _dbContext.KeyWords.AddRange(existingKeywords);
        await _dbContext.SaveChangesAsync();

        // Create Excel with only one of the existing words and one new word
        var testData = new List<(string, string)>
        {
            ("1111111111", "слово1"), // Existing word
            ("4444444444", "новое слово") // New word
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2)); // Only the words from Excel file are returned

        // Verify all keywords are preserved in database (existing unlisted ones + new ones)
        var allKeywords = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync();
        Assert.That(allKeywords.Count, Is.EqualTo(4)); // 3 original + 1 new

        // Verify existing unlisted keywords are preserved
        var preservedKeyword2 = allKeywords.FirstOrDefault(k => k.Word == "слово2");
        Assert.That(preservedKeyword2, Is.Not.Null);
        Assert.That(preservedKeyword2!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("2222222222"));

        var preservedKeyword3 = allKeywords.FirstOrDefault(k => k.Word == "слово3");
        Assert.That(preservedKeyword3, Is.Not.Null);
        Assert.That(preservedKeyword3!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("3333333333"));

        // Verify the uploaded keywords are present
        var updatedKeyword1 = allKeywords.FirstOrDefault(k => k.Word == "слово1");
        Assert.That(updatedKeyword1, Is.Not.Null);
        Assert.That(updatedKeyword1!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("1111111111"));

        var newKeyword = allKeywords.FirstOrDefault(k => k.Word == "новое слово");
        Assert.That(newKeyword, Is.Not.Null);
        Assert.That(newKeyword!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("4444444444"));
        Assert.That(newKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase)); // Contains space, so it's a phrase
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

        var savedKeywords = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(4));
        
        // Check each word was processed correctly
        Assert.That(savedKeywords.Any(k => k.Word == "слово1" && k.MatchTypeId == (int)WordMatchTypeCode.WeakMorphology));
        Assert.That(savedKeywords.Any(k => k.Word == "слово2" && k.MatchTypeId == (int)WordMatchTypeCode.WeakMorphology));
        Assert.That(savedKeywords.Any(k => k.Word == "фраза с пробелами" && k.MatchTypeId == (int)WordMatchTypeCode.Phrase));
        Assert.That(savedKeywords.Any(k => k.Word == "ещё одна фраза" && k.MatchTypeId == (int)WordMatchTypeCode.Phrase));
        
        // Verify all have the same FeacnCode
        foreach (var keyword in savedKeywords)
        {
            Assert.That(keyword.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("1234567890"));
        }
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

    [Test]
    public async Task UploadKeywordsFromExcelAsync_DuplicateWords_ShouldConsolidateFeacnCodes()
    {
        // Arrange
        var testData = new List<(string, string)>
        {
            ("1234567890", "тестовое слово"),
            ("0987654321", "тестовое слово"), // Duplicate word with different code
            ("5555555555", "тестовое слово")  // Same word with yet another code
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(1)); // Only one keyword entry despite 3 rows in Excel

        // Verify the keyword was consolidated with all FEACN codes
        var keyword = result.First();
        Assert.That(keyword.Word, Is.EqualTo("тестовое слово"));
        Assert.That(keyword.KeyWordFeacnCodes.Count, Is.EqualTo(3)); // All three codes should be present
        
        var feacnCodes = keyword.KeyWordFeacnCodes.Select(fc => fc.FeacnCode).ToList();
        Assert.That(feacnCodes, Contains.Item("1234567890"));
        Assert.That(feacnCodes, Contains.Item("0987654321")); 
        Assert.That(feacnCodes, Contains.Item("5555555555"));

        // Verify in database
        var savedKeywords = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(1));
        
        var savedKeyword = savedKeywords.First();
        Assert.That(savedKeyword.Word, Is.EqualTo("тестовое слово"));
        Assert.That(savedKeyword.KeyWordFeacnCodes.Count, Is.EqualTo(3));
        Assert.That(savedKeyword.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase)); // Contains space, so it's a phrase
        
        var savedFeacnCodes = savedKeyword.KeyWordFeacnCodes.Select(fc => fc.FeacnCode).ToList();
        Assert.That(savedFeacnCodes, Contains.Item("1234567890"));
        Assert.That(savedFeacnCodes, Contains.Item("0987654321"));
        Assert.That(savedFeacnCodes, Contains.Item("5555555555"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldCreateFeacnInsertItems()
    {
        // Arrange
        var testData = new List<(string, string, string?, string?)>
        {
            ("1234567890", "тестовое слово", "до текста", "после текста"),
            ("0987654321", "другое слово", null, "только после"),
            ("5555555555", "третье слово", "только до", null),
            ("6666666666", "четвертое слово", null, null) // No insert values
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(4)); // 4 keywords processed

        // Verify FEACN insert items were created (only for first 3 rows with non-null values)
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(3)); // Only 3 have insert values

        // Verify first item (both values)
        var item1 = feacnInsertItems.FirstOrDefault(item => item.Code == "1234567890");
        Assert.That(item1, Is.Not.Null);
        Assert.That(item1!.InsertBefore, Is.EqualTo("до текста"));
        Assert.That(item1.InsertAfter, Is.EqualTo("после текста"));

        // Verify second item (only after)
        var item2 = feacnInsertItems.FirstOrDefault(item => item.Code == "0987654321");
        Assert.That(item2, Is.Not.Null);
        Assert.That(item2!.InsertBefore, Is.Null);
        Assert.That(item2.InsertAfter, Is.EqualTo("только после"));

        // Verify third item (only before)
        var item3 = feacnInsertItems.FirstOrDefault(item => item.Code == "5555555555");
        Assert.That(item3, Is.Not.Null);
        Assert.That(item3!.InsertBefore, Is.EqualTo("только до"));
        Assert.That(item3.InsertAfter, Is.Null);

        // Verify no item for fourth row (no insert values)
        var item4 = feacnInsertItems.FirstOrDefault(item => item.Code == "6666666666");
        Assert.That(item4, Is.Null);
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldUpdateExistingItems()
    {
        // Arrange - Add existing FEACN insert item
        var existingItem = new FeacnInsertItem
        {
            Code = "1234567890",
            InsertBefore = "старое до",
            InsertAfter = "старое после"
        };
        _dbContext.FeacnInsertItems.Add(existingItem);
        await _dbContext.SaveChangesAsync();

        var testData = new List<(string, string, string?, string?)>
        {
            ("1234567890", "тестовое слово", "новое до", null) // Update existing item - overwrite both fields
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1)); // Still only one item

        var updatedItem = feacnInsertItems.First();
        Assert.That(updatedItem.Code, Is.EqualTo("1234567890"));
        Assert.That(updatedItem.InsertBefore, Is.EqualTo("новое до")); // Updated
        Assert.That(updatedItem.InsertAfter, Is.Null); // Overwritten to null
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldTrimsWhitespace()
    {
        // Arrange
        var testData = new List<(string, string, string?, string?)>
        {
            ("1234567890", "тестовое слово", "  до с пробелами  ", "  после с пробелами  ")
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1));

        var item = feacnInsertItems.First();
        Assert.That(item.InsertBefore, Is.EqualTo("до с пробелами")); // Trimmed
        Assert.That(item.InsertAfter, Is.EqualTo("после с пробелами")); // Trimmed
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldTreatEmptyStringAsNull()
    {
        // Arrange
        var testData = new List<(string, string, string?, string?)>
        {
            ("1234567890", "тестовое слово", "", "   ") // Empty and whitespace-only values
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(0)); // No item created since both values are effectively null
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithOnlyInsertBeforeColumn_ShouldWork()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        
        // Header with only insertBefore column
        worksheet.Cell(1, 1).Value = "Код";
        worksheet.Cell(1, 2).Value = "Наименование";
        worksheet.Cell(1, 3).Value = "Перед описанием";
        
        // Data
        worksheet.Cell(2, 1).Value = "1234567890";
        worksheet.Cell(2, 2).Value = "тестовое слово";
        worksheet.Cell(2, 3).Value = "только до";
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var excelBytes = ms.ToArray();

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1));

        var item = feacnInsertItems.First();
        Assert.That(item.Code, Is.EqualTo("1234567890"));
        Assert.That(item.InsertBefore, Is.EqualTo("только до"));
        Assert.That(item.InsertAfter, Is.Null);
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithOnlyInsertAfterColumn_ShouldWork()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        
        // Header with only insertAfter column
        worksheet.Cell(1, 1).Value = "Код";
        worksheet.Cell(1, 2).Value = "Наименование";
        worksheet.Cell(1, 3).Value = "В конце описания";
        
        // Data
        worksheet.Cell(2, 1).Value = "1234567890";
        worksheet.Cell(2, 2).Value = "тестовое слово";
        worksheet.Cell(2, 3).Value = "только после";
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var excelBytes = ms.ToArray();

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1));

        var item = feacnInsertItems.First();
        Assert.That(item.Code, Is.EqualTo("1234567890"));
        Assert.That(item.InsertBefore, Is.Null);
        Assert.That(item.InsertAfter, Is.EqualTo("только после"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithoutInsertColumns_ShouldNotAffectFeacnInsertItems()
    {
        // Arrange - Add existing FEACN insert item
        var existingItem = new FeacnInsertItem
        {
            Code = "1234567890",
            InsertBefore = "существующее до",
            InsertAfter = "существующее после"
        };
        _dbContext.FeacnInsertItems.Add(existingItem);
        await _dbContext.SaveChangesAsync();

        // Create Excel without insert columns
        var testData = new List<(string, string)>
        {
            ("1234567890", "тестовое слово"),
            ("0987654321", "другое слово")
        };
        var excelBytes = CreateTestExcelFile(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1)); // Existing item preserved

        var item = feacnInsertItems.First();
        Assert.That(item.Code, Is.EqualTo("1234567890"));
        Assert.That(item.InsertBefore, Is.EqualTo("существующее до")); // Unchanged
        Assert.That(item.InsertAfter, Is.EqualTo("существующее после")); // Unchanged
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldHandleNineDigitCodes()
    {
        // Arrange
        var testData = new List<(string, string, string?, string?)>
        {
            ("123456789", "тестовое слово", "до текста", "после текста") // 9 digits, should be padded
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1));

        var item = feacnInsertItems.First();
        Assert.That(item.Code, Is.EqualTo("0123456789")); // Padded with zero
        Assert.That(item.InsertBefore, Is.EqualTo("до текста"));
        Assert.That(item.InsertAfter, Is.EqualTo("после текста"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_WithFeacnInsertColumns_ShouldHandleMultipleRowsWithSameCode()
    {
        // Arrange - Multiple rows with same code but different insert values
        var testData = new List<(string, string, string?, string?)>
        {
            ("1234567890", "первое слово", "до1", "после1"),
            ("1234567890", "второе слово", "до2", "после2") // Same code, should overwrite
        };
        var excelBytes = CreateTestExcelFileWithInsertColumns(testData);

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "test.xlsx");

        // Assert
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(1)); // Only one item for the code

        var item = feacnInsertItems.First();
        Assert.That(item.Code, Is.EqualTo("1234567890"));
        // Should have the values from the last processed row
        Assert.That(item.InsertBefore, Is.EqualTo("до2"));
        Assert.That(item.InsertAfter, Is.EqualTo("после2"));
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_IntegrationTest_ShouldProcessKeywordsAndFeacnInsertItems()
    {
        // Arrange - Create a comprehensive Excel file with all features
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        
        // Headers
        worksheet.Cell(1, 1).Value = "Код";
        worksheet.Cell(1, 2).Value = "Наименование";
        worksheet.Cell(1, 3).Value = "Перед описанием";
        worksheet.Cell(1, 4).Value = "В конце описания";
        
        // Test data with mixed scenarios
        worksheet.Cell(2, 1).Value = "1234567890"; // Keywords + insert items
        worksheet.Cell(2, 2).Value = "обувь, кроссовки";
        worksheet.Cell(2, 3).Value = "Спортивная";
        worksheet.Cell(2, 4).Value = "для активного отдыха";
        
        worksheet.Cell(3, 1).Value = "987654321"; // Keywords only (9 digits, will be padded)
        worksheet.Cell(3, 2).Value = "одежда, рубашка";
        worksheet.Cell(3, 3).Value = "";
        worksheet.Cell(3, 4).Value = "";
        
        worksheet.Cell(4, 1).Value = "5555555555"; // Insert items only
        worksheet.Cell(4, 2).Value = "аксессуары";
        worksheet.Cell(4, 3).Value = "Модные";
        worksheet.Cell(4, 4).Value = "";
        
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var excelBytes = ms.ToArray();

        // Act
        var result = await _service.UploadKeywordsFromExcelAsync(excelBytes, "integration_test.xlsx");

        // Assert Keywords
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(5)); // обувь, кроссовки, одежда, рубашка, аксессуары

        var savedKeywords = await _dbContext.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync();
        Assert.That(savedKeywords.Count, Is.EqualTo(5));

        // Verify specific keywords
        var obuvKeyword = savedKeywords.FirstOrDefault(k => k.Word == "обувь");
        Assert.That(obuvKeyword, Is.Not.Null);
        Assert.That(obuvKeyword!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("1234567890"));

        var odezhKeyword = savedKeywords.FirstOrDefault(k => k.Word == "одежда");
        Assert.That(odezhKeyword, Is.Not.Null);
        Assert.That(odezhKeyword!.KeyWordFeacnCodes.First().FeacnCode, Is.EqualTo("0987654321")); // Padded

        // Assert FEACN Insert Items
        var feacnInsertItems = await _dbContext.FeacnInsertItems.ToListAsync();
        Assert.That(feacnInsertItems.Count, Is.EqualTo(2)); // Only rows 1 and 3 have insert values

        var insertItem1 = feacnInsertItems.FirstOrDefault(item => item.Code == "1234567890");
        Assert.That(insertItem1, Is.Not.Null);
        Assert.That(insertItem1!.InsertBefore, Is.EqualTo("Спортивная"));
        Assert.That(insertItem1.InsertAfter, Is.EqualTo("для активного отдыха"));

        var insertItem2 = feacnInsertItems.FirstOrDefault(item => item.Code == "5555555555");
        Assert.That(insertItem2, Is.Not.Null);
        Assert.That(insertItem2!.InsertBefore, Is.EqualTo("Модные"));
        Assert.That(insertItem2.InsertAfter, Is.Null);

        // Verify no insert item for the row with empty values
        var noInsertItem = feacnInsertItems.FirstOrDefault(item => item.Code == "0987654321");
        Assert.That(noInsertItem, Is.Null);
    }
}

