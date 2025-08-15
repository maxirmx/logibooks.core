using ClosedXML.Excel;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

public class KeywordsProcessingServiceTests
{
# pragma warning disable CS8618
    private AppDbContext _dbContext;
    private KeywordsProcessingService _service;
# pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"kw_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<KeywordsProcessingService>();
        _service = new KeywordsProcessingService(_dbContext, logger);
    }

    [Test]
    public async Task UploadKeywordsFromExcelAsync_AddsUpdatesAndDeletes()
    {
        _dbContext.KeyWords.Add(new KeyWord { Id = 1, Word = "старый", MatchTypeId = (int)WordMatchTypeCode.WeakMorphology, FeacnCode = "1111111111" });
        _dbContext.KeyWords.Add(new KeyWord { Id = 2, Word = "удалить", MatchTypeId = (int)WordMatchTypeCode.Phrase, FeacnCode = "2222222222" });
        _dbContext.SaveChanges();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");
        ws.Cell(1, 1).Value = "код";
        ws.Cell(1, 2).Value = "наименование";
        ws.Cell(2, 1).Value = "1234567890";
        ws.Cell(2, 2).Value = "старый, новый товар";
        ws.Cell(3, 1).Value = "0987654321";
        ws.Cell(3, 2).Value = "еще слово";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var content = ms.ToArray();

        var result = await _service.UploadKeywordsFromExcelAsync(content, "test.xlsx");

        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(_dbContext.KeyWords.Count(), Is.EqualTo(3));
        Assert.That(_dbContext.KeyWords.Any(k => k.Word == "удалить"), Is.False);

        var updated = _dbContext.KeyWords.Single(k => k.Word == "старый");
        Assert.That(updated.FeacnCode, Is.EqualTo("1234567890"));
        Assert.That(updated.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.WeakMorphology));

        var phrase = _dbContext.KeyWords.Single(k => k.Word == "новый товар");
        Assert.That(phrase.MatchTypeId, Is.EqualTo((int)WordMatchTypeCode.Phrase));
        Assert.That(phrase.FeacnCode, Is.EqualTo("1234567890"));
    }

    [Test]
    public void UploadKeywordsFromExcelAsync_InvalidCode_Throws()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Лист1");
        ws.Cell(1, 1).Value = "код";
        ws.Cell(1, 2).Value = "наименование";
        ws.Cell(2, 1).Value = "12345"; // invalid
        ws.Cell(2, 2).Value = "товар";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var content = ms.ToArray();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UploadKeywordsFromExcelAsync(content, "bad.xlsx"));
        Assert.That(ex!.Message, Does.Contain("должен содержать ровно 10 цифр"));
    }
}

