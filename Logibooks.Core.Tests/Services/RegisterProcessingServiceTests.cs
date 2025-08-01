using Logibooks.Core.Data;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using ExcelDataReader.Exceptions;
using ExcelDataReader;
using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Services;

public class ConvertValueToPropertyTypeTests
{
#pragma warning disable CS8618
    private RegisterProcessingService _service;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        // Create a minimal DbContextOptions for AppDbContext
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
            .Options;

        // Pass the valid options to the AppDbContext constructor
        var dbContext = new AppDbContext(options);

        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(dbContext, logger);
    }

    [TestCase("42", typeof(int), 42)]
    [TestCase("notanint", typeof(int), 0)]
    [TestCase("3.14", typeof(double), 3.14)]
    [TestCase("2,71", typeof(double), 2.71)] // comma as decimal separator
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
        var result = typeof(RegisterProcessingService)
            .GetMethod("ConvertValueToPropertyType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { input, type, "TestProp" });

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
        var result = typeof(RegisterProcessingService)
            .GetMethod("ConvertValueToPropertyType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { null, type, "TestProp" });
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_NullableDouble_ReturnsNullOnEmpty()
    {
        var type = typeof(double?);
        var result = typeof(RegisterProcessingService)
            .GetMethod("ConvertValueToPropertyType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { "", type, "TestProp" });
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_UsesChangeType()
    {
        var type = typeof(long);
        var result = typeof(RegisterProcessingService)
            .GetMethod("ConvertValueToPropertyType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { "123456789", type, "TestProp" });
        Assert.That(result, Is.EqualTo(123456789L));
    }

    [Test]
    public void ConvertValueToPropertyType_UnknownType_ReturnsDefaultOnError()
    {
        var type = typeof(Guid);
        var result = typeof(RegisterProcessingService)
            .GetMethod("ConvertValueToPropertyType", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_service, new object?[] { "notaguid", type, "TestProp" });
        Assert.That(result, Is.EqualTo(Guid.Empty));
    }

}

public class UploadOzonRegisterTests
{
#pragma warning disable CS8618
    private RegisterProcessingService _service;
    private AppDbContext _dbContext;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ozon_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(_dbContext, logger);

        // Add Uzbekistan to Countries
        _dbContext.Countries.Add(new Country
        {
            IsoAlpha2 = "UZ",
            NameRuShort = "Узбекистан",
            IsoNumeric = 860
        });
        _dbContext.SaveChanges();
    }

    [Test]
    public async Task UploadOzonRegisterFromExcelAsync_InsertsOrders()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));

        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId() , content, "Озон_Short.xlsx");

        var ctx = (AppDbContext)typeof(RegisterProcessingService)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_service)!;

        var register = ctx.Registers.FirstOrDefault(r => r.Id == reference.Id);
        Assert.That(reference.Id, Is.GreaterThan(0));
        Assert.That(ctx.Registers.Count(), Is.EqualTo(1));
        Assert.That(ctx.OzonOrders.Count(), Is.EqualTo(3));
        Assert.That(ctx.OzonOrders.OrderBy(o => o.Id).First().PostingNumber, Is.EqualTo("0180993146-0049-7"));
        Assert.That(register, Is.Not.Null);
        Assert.That(register!.DestCountryCode, Is.EqualTo(860));
    }
}

public class UploadWbrRegisterTests
{
#pragma warning disable CS8618
    private RegisterProcessingService _service;
    private AppDbContext _dbContext;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wbr_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(_dbContext, logger);

        // Add Uzbekistan to Countries
        _dbContext.Countries.Add(new Country
        {
            IsoAlpha2 = "UZ",
            NameRuShort = "Узбекистан",
            IsoNumeric = 860
        });
        _dbContext.SaveChanges();
    }

    [Test]
    public async Task UploadWbrRegisterFromExcelAsync_InsertsOrders()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Реестр_207730349.xlsx"));

        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), content, "Реестр_207730349.xlsx");

        var ctx = (AppDbContext)typeof(RegisterProcessingService)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_service)!;

        var register = ctx.Registers.FirstOrDefault(r => r.Id == reference.Id);
        Assert.That(reference.Id, Is.GreaterThan(0));
        Assert.That(ctx.Registers.Count(), Is.EqualTo(1));
        Assert.That(ctx.WbrOrders.Count(), Is.EqualTo(3));
        Assert.That(ctx.WbrOrders.OrderBy(o => o.Id).First().RowNumber, Is.EqualTo(3101));
        Assert.That(register, Is.Not.Null);
        Assert.That(register!.DestCountryCode, Is.EqualTo(860));
    }
}

public class UploadRegisterErrorTests
{
#pragma warning disable CS8618
    private RegisterProcessingService _service;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"err_{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(dbContext, logger);
    }

    [Test]
    public void UploadRegisterFromExcelAsync_InvalidFile_ThrowsHeaderException()
    {
        var content = File.ReadAllBytes(Path.Combine("test.data", "file.txt"));
        Assert.ThrowsAsync<ExcelDataReader.Exceptions.HeaderException>(async () =>
            await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "file.txt"));
    }

    [Test]
    public void UploadRegisterFromExcelAsync_EmptyExcel_ThrowsInvalidOperationException()
    {
        var content = File.ReadAllBytes(Path.Combine("test.data", "Register_Empty.xlsx"));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Register_Empty.xlsx"));
    }

    [Test]
    public void UploadRegisterFromExcelAsync_MissingMapping_ThrowsFileNotFoundException()
    {
        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", "wbr_register_mapping.yaml");
        var backup = mappingPath + ".bak";
        File.Move(mappingPath, backup);
        try
        {
            var content = File.ReadAllBytes(Path.Combine("test.data", "Реестр_207730349.xlsx"));
            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), content, "Реестр_207730349.xlsx"));
        }
        finally
        {
            File.Move(backup, mappingPath);
        }
    }
}

public class DownloadRegisterTests
{
#pragma warning disable CS8618
    private RegisterProcessingService _service;
    private AppDbContext _dbContext;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"download_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        var logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(_dbContext, logger);

        _dbContext.Countries.Add(new Country
        {
            IsoAlpha2 = "UZ",
            NameRuShort = "Узбекистан",
            IsoNumeric = 860
        });
        _dbContext.SaveChanges();
    }

    [Test]
    public async Task DownloadWbrRegister_ReturnsExcel()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Реестр_207730349.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), content, "Реестр_207730349.xlsx");

        var first = _dbContext.WbrOrders.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)OrderCheckStatusCode.HasIssues;
        await _dbContext.SaveChangesAsync();

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        Assert.That(table.Rows.Count, Is.EqualTo(4));
        Assert.That(table.Rows[1][0].ToString(), Is.EqualTo(first.RowNumber.ToString()));
    }

    [Test]
    public async Task DownloadOzonRegister_ReturnsExcel()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonOrders.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)OrderCheckStatusCode.HasIssues;
        await _dbContext.SaveChangesAsync();

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        Assert.That(table.Rows.Count, Is.EqualTo(4));
        Assert.That(table.Rows[1][2].ToString(), Is.EqualTo(first.PostingNumber));

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("xl/styles.xml");
        using var sr = new StreamReader(entry!.Open());
        var styles = sr.ReadToEnd();
        Assert.That(styles.Contains("FFFF0000"), Is.True);
    }
}
