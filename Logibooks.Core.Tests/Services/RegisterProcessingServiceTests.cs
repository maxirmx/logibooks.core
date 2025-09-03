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
using ClosedXML.Excel;

namespace Logibooks.Core.Tests.Services;

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
        Assert.That(ctx.OzonParcels.Count(), Is.EqualTo(3));
        Assert.That(ctx.OzonParcels.OrderBy(o => o.Id).First().PostingNumber, Is.EqualTo("0180993146-0049-7"));
        Assert.That(register, Is.Not.Null);
        Assert.That(register!.TheOtherCountryCode, Is.EqualTo(860));
    }

    [Test]
    public async Task UploadOzonRegisterFromExcelAsync_MarkedRowsSetStatusAndColor()
    {
        var file = Path.Combine("test.data", "Озон_Short.xlsx");
        var bytes = await File.ReadAllBytesAsync(file);
        using (var ms = new MemoryStream(bytes))
        {
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(1);
            
            // Clear any auto-filters that might cause issues with ClosedXML
            if (ws.AutoFilter != null)
            {
                ws.AutoFilter.Clear();
            }
            
            ws.Row(2).Style.Fill.BackgroundColor = XLColor.Red;
            using var msOut = new MemoryStream();
            wb.SaveAs(msOut);
            bytes = msOut.ToArray();
        }

        await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), bytes, "Озон_Short_colored.xlsx");

        var ctx = (AppDbContext)typeof(RegisterProcessingService)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_service)!;

        var first = ctx.OzonParcels.OrderBy(o => o.Id).First();
        Assert.That(first.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.MarkedByPartner));
        Assert.That(first.PartnerColor, Is.Not.EqualTo(0));
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
        _dbContext.Countries.Add(new Country
        {
            IsoAlpha2 = "RU",
            NameRuShort = "Россия",
            IsoNumeric = 643
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
        Assert.That(ctx.WbrParcels.Count(), Is.EqualTo(3));
        Assert.That(ctx.WbrParcels.OrderBy(o => o.Id).First().RowNumber, Is.EqualTo(3101));
        Assert.That(register, Is.Not.Null);
        Assert.That(register!.TheOtherCountryCode, Is.EqualTo(860));
    }

    [Test]
    public async Task UploadWbrRegisterFromExcelAsync_MarkedRowsSetStatusAndColor()
    {
        var file = Path.Combine("test.data", "Реестр_207730349.xlsx");
        var bytes = await File.ReadAllBytesAsync(file);
        using (var ms = new MemoryStream(bytes))
        {
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(1);
            
            // Clear any auto-filters that might cause issues with ClosedXML
            if (ws.AutoFilter != null)
            {
                ws.AutoFilter.Clear();
            }
            
            ws.Row(2).Style.Fill.BackgroundColor = XLColor.Red;
            using var msOut = new MemoryStream();
            wb.SaveAs(msOut);
            bytes = msOut.ToArray();
        }

        await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), bytes, "Реестр_207730349_colored.xlsx");

        var ctx = (AppDbContext)typeof(RegisterProcessingService)
            .GetField("_db", BindingFlags.NonPublic | BindingFlags.Instance)! 
            .GetValue(_service)!;

        Assert.That(ctx.WbrParcels.Any(o => o.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner && o.PartnerColor != 0));
        Assert.That(ctx.WbrParcels.Any(o => o.CheckStatusId == (int)ParcelCheckStatusCode.NotChecked && o.PartnerColor == 0));
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
        _dbContext.Countries.Add(new Country
        {
            IsoAlpha2 = "RU",
            NameRuShort = "Россия",
            IsoNumeric = 643
        });
        _dbContext.SaveChanges();
    }

    [Test]
    public async Task DownloadWbrRegister_ReturnsExcel()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Реестр_207730349.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), content, "Реестр_207730349.xlsx");

        var first = _dbContext.WbrParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.HasIssues;
        await _dbContext.SaveChangesAsync();

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        Assert.That(table.Rows.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task DownloadOzonRegister_ReturnsExcel()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.HasIssues;
        await _dbContext.SaveChangesAsync();

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        Assert.That(table.Rows.Count, Is.EqualTo(4));

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("xl/styles.xml");
        using var sr = new StreamReader(entry!.Open());
        var styles = sr.ReadToEnd();
        Assert.That(styles.Contains("FFFF0000"), Is.True);
    }

    [Test]
    public async Task DownloadRegister_MarkedByPartner_UsesPartnerColor()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner;
        first.PartnerColorXL = XLColor.FromArgb(0, 255, 0);
        await _dbContext.SaveChangesAsync();

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var entry = archive.GetEntry("xl/styles.xml");
        using var sr = new StreamReader(entry!.Open());
        var styles = sr.ReadToEnd();
        Assert.That(styles.Contains("FF00FF00"), Is.True);
        Assert.That(styles.Contains("FFFF0000"), Is.False);
    }

    [Test]
    public async Task DownloadRegister_ApprovedWithExcise_UsesOrangeColor()
    {
        // Arrange: Create test data with ApprovedWithExcise status
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.ApprovedWithExcise;
        await _dbContext.SaveChangesAsync();

        // Act: Download Excel with the ApprovedWithExcise status
        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        // Assert: Verify orange color is applied
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);
        
        // Find the row with ApprovedWithExcise status (should be row 2 since row 1 is header)
        var row = worksheet.Row(2);
        var backgroundColor = row.Style.Fill.BackgroundColor;
        
        Assert.That(backgroundColor, Is.EqualTo(XLColor.Orange), "ApprovedWithExcise status should use orange background color");
    }

    [Test]
    public async Task DownloadRegister_ApprovedWithExcise_WbrOrders_UsesOrangeColor()
    {
        // Arrange: Create test data with WBR orders and ApprovedWithExcise status
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Реестр_207730349.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetWBRId(), content, "Реестр_207730349.xlsx");

        var first = _dbContext.WbrParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.ApprovedWithExcise;
        await _dbContext.SaveChangesAsync();

        // Act: Download Excel with the ApprovedWithExcise status
        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        // Assert: Verify orange color is applied
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);
        
        // Find the row with ApprovedWithExcise status (should be row 2 since row 1 is header)
        var row = worksheet.Row(2);
        var backgroundColor = row.Style.Fill.BackgroundColor;
        
        Assert.That(backgroundColor, Is.EqualTo(XLColor.Orange), "ApprovedWithExcise status should use orange background color for WBR orders");
    }


    [Test]
    public async Task DownloadRegister_HasIssuesRange_UsesRedColor()
    {
        // Arrange: Test various HasIssues status codes
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var orders = _dbContext.OzonParcels.OrderBy(o => o.Id).ToList();
        
        // Set different HasIssues status codes
        orders[0].CheckStatusId = (int)ParcelCheckStatusCode.HasIssues; 
        orders[1].CheckStatusId = (int)ParcelCheckStatusCode.InvalidFeacnFormat; 
        orders[2].CheckStatusId = (int)ParcelCheckStatusCode.BlockedByFeacnCodeAndStopWord;
        orders[3].CheckStatusId = (int)ParcelCheckStatusCode.BlockedByFeacnCode;
        orders[4].CheckStatusId = (int)ParcelCheckStatusCode.BlockedByStopWord;

        await _dbContext.SaveChangesAsync();

        // Act: Download Excel
        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        // Assert: All should have red background
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);
        
        for (int i = 2; i <= 5; i++) // Rows 2-54 (row 1 is header)
        {
            var row = worksheet.Row(i);
            Assert.That(row.Style.Fill.BackgroundColor, Is.EqualTo(XLColor.Red), 
                $"Row {i} with HasIssues status should be red");
        }
    }

    [Test]
    public async Task DownloadRegister_NoIssuesStatus_NoColorApplied()
    {
        // Arrange: Create test data with NoIssues status
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.NoIssues;
        await _dbContext.SaveChangesAsync();

        // Act: Download Excel
        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        // Assert: No special color should be applied (should be default/transparent)
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);
        
        var row = worksheet.Row(2);
        var backgroundColor = row.Style.Fill.BackgroundColor;
        
        // The background should be either NoColor, Transparent, or default
        Assert.That(backgroundColor, Is.Not.EqualTo(XLColor.Orange).And.Not.EqualTo(XLColor.Red), 
            "NoIssues status should not apply any special color");
    }

    [Test]
    public async Task DownloadRegister_MarkedByPartnerWithoutColor_StillGetsMarkedStatus()
    {
        // Arrange: Test MarkedByPartner without partner color set
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var first = _dbContext.OzonParcels.OrderBy(o => o.Id).First();
        first.CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner;
        first.PartnerColor = 0; // No partner color set
        await _dbContext.SaveChangesAsync();

        // Act: Download Excel
        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        // Assert: Since PartnerColor is 0, no color should be applied but logic should still work
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var worksheet = workbook.Worksheet(1);
        
        var row = worksheet.Row(2);
        // The condition checks if PartnerColor != 0, so no color should be applied
        Assert.That(row.Style.Fill.BackgroundColor, Is.Not.EqualTo(XLColor.Orange).And.Not.EqualTo(XLColor.Red), 
            "MarkedByPartner with PartnerColor=0 should not apply any color");
    }

    [Test]
    public async Task DownloadRegister_PreservesOrder()
    {
        var content = await File.ReadAllBytesAsync(Path.Combine("test.data", "Озон_Short.xlsx"));
        var reference = await _service.UploadRegisterFromExcelAsync(_service.GetOzonId(), content, "Озон_Short.xlsx");

        var bytes = await _service.DownloadRegisterToExcelAsync(reference.Id);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(bytes);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        var postings = new[]
        {
            table.Rows[1][2]?.ToString(),
            table.Rows[2][2]?.ToString(),
            table.Rows[3][2]?.ToString()
        };

        Assert.That(postings, Is.EqualTo(new[]
        {
            "0180993146-0049-7",
            "0180993146-0049-6",
            "0208022828-0010-1"
        }));
    }

    [Test]
    public async Task DownloadRegister_WritesAlpha2CountryCode()
    {
        // Arrange: Use country already created in Setup
        var country = _dbContext.Countries.First(c => c.IsoNumeric == 860);

        var oRegister = new Register { FileName = "oreg.xlsx", CompanyId = _service.GetOzonId() };
        var wRegister = new Register { FileName = "wreg.xlsx", CompanyId = _service.GetWBRId() };
        _dbContext.Registers.Add(oRegister);
        _dbContext.Registers.Add(wRegister);
        await _dbContext.SaveChangesAsync();

        var wOrder = new WbrParcel
        {
            RegisterId = wRegister.Id,
            CountryCode = country.IsoNumeric,
            ProductName = "Test",
            TnVed = "12345678",
            StatusId = 1,
            CheckStatusId = 1
        };
        _dbContext.WbrParcels.Add(wOrder);

        var oOrder = new OzonParcel
        {
            RegisterId = oRegister.Id,
            CountryCode = country.IsoNumeric,
            ProductName = "Test",
            TnVed = "12345678",
            StatusId = 1,
            CheckStatusId = 1
        };
        _dbContext.OzonParcels.Add(oOrder);

        await _dbContext.SaveChangesAsync();

        // Act: Download Excel
        foreach (int c in new[] { wRegister.Id, oRegister.Id })
        {
            var bytes = await _service.DownloadRegisterToExcelAsync(c);
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var ms = new MemoryStream(bytes);
            using var reader = ExcelReaderFactory.CreateReader(ms);
            var ds = reader.AsDataSet();
            var table = ds.Tables[0];

            // Find the CountryCode column index
            int countryCol = -1;
            for (int i = 0; i < table.Columns.Count; i++)
            {
                if (table.Rows[0][i]?.ToString()?.ToLowerInvariant().Contains("страна") == true)
                {
                    countryCol = i;
                    break;
                }
            }
            Assert.That(countryCol, Is.GreaterThanOrEqualTo(0), "CountryCode column not found");
            // Assert: The cell contains the alpha2 code
            var cellValue = table.Rows[1][countryCol]?.ToString();
            Assert.That(cellValue, Is.EqualTo("UZ"));
        }
    }
}
