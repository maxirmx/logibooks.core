using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Logibooks.Core.Data;
using Logibooks.Core.Services;
using Logibooks.Core.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class RegisterProcessingServiceTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private RegisterProcessingService _service;
#pragma warning restore CS8618
    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"service_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(_dbContext, logger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task ProcessExcel_ReturnsEmptyError_WhenExcelFileIsEmpty()
    {
        string testFilePath = Path.Combine(testDataDir, "Register_Empty.xlsx");
        byte[] excelContent = File.ReadAllBytes(testFilePath);
        var result = await _service.ProcessExcelAsync(excelContent, "Register_Empty.xlsx");
        Assert.That(result.Error, Is.EqualTo(ProcessExcelError.EmptyExcel));
    }

    [Test]
    public async Task ProcessExcel_ReturnsMappingNotFound_WhenMappingFileMissing()
    {
        byte[] excelContent = [0x50, 0x4B, 0x03, 0x04];
        var result = await _service.ProcessExcelAsync(excelContent, "test.xlsx", "non_existent_mapping.yaml");
        Assert.That(result.Error, Is.EqualTo(ProcessExcelError.MappingNotFound));
        Assert.That(result.MappingPath, Does.Contain("non_existent_mapping.yaml"));
    }

    [TestCase("42", typeof(int), 42)]
    [TestCase("notanint", typeof(int), 0)]
    [TestCase("3.14", typeof(double), 3.14)]
    [TestCase("2,71", typeof(double), 2.71)]
    [TestCase("notadouble", typeof(double), 0.0)]
    [TestCase("123.45", typeof(decimal), 123.45)]
    [TestCase("67,89", typeof(decimal), 67.89)]
    [TestCase("notadecimal", typeof(decimal), 0.0)]
    [TestCase("true", typeof(bool), true)]
    [TestCase("false", typeof(bool), false)]
    [TestCase("1", typeof(bool), true)]
    [TestCase("0", typeof(bool), false)]
    [TestCase("yeS", typeof(bool), true)]
    [TestCase("no", typeof(bool), false)]
    [TestCase("Да", typeof(bool), true)]
    [TestCase("нет", typeof(bool), false)]
    [TestCase("", typeof(bool), false)]
    [TestCase("notabool", typeof(bool), false)]
    [TestCase("2024-06-28", typeof(DateTime), "2024-06-28")]
    [TestCase("notadate", typeof(DateTime), "0001-01-01")]
    [TestCase("2024-06-28", typeof(DateOnly), "2024-06-28")]
    [TestCase("2024-06-28T13:00:12", typeof(DateOnly), "2024-06-28")]
    [TestCase("notadate", typeof(DateOnly), "0001-01-01")]
    [TestCase("hello", typeof(string), "hello")]
    [TestCase("", typeof(string), "")]
    [TestCase(null, typeof(string), "")]
    public void ConvertValueToPropertyType_PrimitiveTypes_Works(string? input, Type type, object expected)
    {
        var result = _service.ConvertValueToPropertyType(input, type, "TestProp");
        if (type == typeof(DateTime))
        {
            var expectedDate = DateTime.TryParse(expected.ToString(), out var dt) ? dt : default;
            Assert.That(result, Is.EqualTo(expectedDate));
        }
        else if (type == typeof(DateOnly))
        {
            var expectedDate = DateOnly.TryParse(expected.ToString(), out var d) ? d : default;
            Assert.That(result, Is.EqualTo(expectedDate));
        }
        else
        {
            Assert.That(result, Is.EqualTo(expected));
        }
    }

    [Test]
    public void ConvertValueToPropertyType_NullableInt_ReturnsNullOnNull()
    {
        var type = typeof(int?);
        var result = _service.ConvertValueToPropertyType(null, type, "TestProp");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_NullableDouble_ReturnsNullOnEmpty()
    {
        var type = typeof(double?);
        var result = _service.ConvertValueToPropertyType("", type, "TestProp");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_UsesChangeType()
    {
        var type = typeof(long);
        var result = _service.ConvertValueToPropertyType("123456789", type, "TestProp");
        Assert.That(result, Is.EqualTo(123456789L));
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_ReturnsDefaultOnError()
    {
        var type = typeof(Guid);
        var result = _service.ConvertValueToPropertyType("notaguid", type, "TestProp");
        Assert.That(result, Is.EqualTo(Guid.Empty));
    }
}
