using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class ParcelIndPostGeneratorTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private AppDbContext _dbContext;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"xml_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        // Seed with real values from AppDbContext (using RegistersController as reference)
        _dbContext.Countries.Add(new Country
        {
            IsoNumeric = 643,
            IsoAlpha2 = "RU",
            NameRuShort = "Российская Федерация"
        });
        _dbContext.Countries.Add(new Country
        {
            IsoNumeric = 860,
            IsoAlpha2 = "UZ",
            NameRuShort = "Узбекистан"
        });
        _dbContext.Companies.AddRange(
            new Company
            {
                Id = 1,
                Inn = "7704217370",
                Kpp = "997750001",
                Name = "ООО \"Интернет Решения\"",
                ShortName = "",
                CountryIsoNumeric = 643,
                PostalCode = "123112",
                City = "Москва",
                Street = "Пресненская набережная д.10, пом.1, этаж 41, ком.6"
            },
            new Company
            {
                Id = 2,
                Inn = "9714053621",
                Kpp = "507401001",
                Name = "",
                ShortName = "ООО \"РВБ\"",
                CountryIsoNumeric = 643,
                PostalCode = "",
                City = "д. Коледино",
                Street = "Индустриальный Парк Коледино, д.6, стр.1"
            },
            new Company
            {
                Id = 3,
                Inn = "9999999999",
                Kpp = "999999999",
                Name = "Other Company",
                ShortName = "OtherCo",
                CountryIsoNumeric = 643,
                PostalCode = "000000",
                City = "TestCity",
                Street = "Test Street"
            }
        );
        _dbContext.TransportationTypes.AddRange(
            new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" },
            new TransportationType { Id = 2, Code = TransportationTypeCode.Auto, Name = "Авто" }
        );
        _dbContext.CustomsProcedures.AddRange(
            new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" },
            new CustomsProcedure { Id = 2, Code = 60, Name = "Реимпорт" }
        );
        _dbContext.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GenerateXML_ReturnsXml()
    {
        var register = new Register
        {
            Id = 10,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "real_register.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-2024-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860 // Uzbekistan
        };

        _dbContext.Registers.Add(register);

        var order = new WbrOrder { 
            Id = 3, 
            RegisterId = 10, 
            StatusId = 1, 
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };

        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(3);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
    }

    [Test]
    public async Task GenerateXML_WbrOrder_IndPostApi_CorrectXmlGenerated()
    {
        var register = new Register
        {
            Id = 100,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "wbr_register.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "WBR-INV-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        var order = new WbrOrder
        {
            Id = 101,
            RegisterId = 100,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-SHK-1",
            ProductName = "WBR Product",
            TnVed = "12345678",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrOrders.Add(order);
        _dbContext.SaveChanges();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(101);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        Assert.That(filename, Does.StartWith("IndPost_"));
        Assert.That(xml, Does.Contain("WBR Product"));
    }

    [Test]
    public async Task GenerateXML_OzonOrder_IndPostApi_CorrectXmlGenerated()
    {
        var register = new Register
        {
            Id = 200,
            CompanyId = 1,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "ozon_register.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "OZON-INV-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 643
        };
        _dbContext.Registers.Add(register);
        var order = new OzonOrder
        {
            Id = 201,
            RegisterId = 200,
            StatusId = 1,
            CountryCode = 643,
            PostingNumber = "OZON-PN-1",
            ProductName = "Ozon Product",
            TnVed = "87654321",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.OzonOrders.Add(order);
        _dbContext.SaveChanges();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(201);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        Assert.That(filename, Does.StartWith("IndPost_"));
        Assert.That(xml, Does.Contain("Ozon Product"));
    }

    [Test]
    public async Task GenerateXML4R_WbrOrder_IndPostApi_CreatesZipWithUniqueOrders()
    {
        // Fetch required related entities from the context
        var countryRu = await _dbContext.Countries.FirstAsync(c => c.IsoNumeric == 643);
        var countryUz = await _dbContext.Countries.FirstAsync(c => c.IsoNumeric == 860);
        var company = await _dbContext.Companies.FirstAsync(c => c.Id == 2);
        var transportationType = await _dbContext.TransportationTypes.FirstAsync(t => t.Id == 1);
        var customsProcedure = await _dbContext.CustomsProcedures.FirstAsync(c => c.Id == 1);

        // Ensure company navigation property is set
        company.Country = countryRu;
        _dbContext.Companies.Update(company);
        await _dbContext.SaveChangesAsync();

        var register = new Register {
            Id = 300,
            CompanyId = company.Id,
            Company = company,
            TransportationTypeId = transportationType.Id,
            TransportationType = transportationType,
            CustomsProcedureId = customsProcedure.Id,
            CustomsProcedure = customsProcedure,
            FileName = "wbr_zip.xlsx",
            DTime = DateTime.Now,
            TheOtherCountryCode = countryUz.IsoNumeric,
            TheOtherCountry = countryUz
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var loadedRegister = await _dbContext.Registers
            .Include(r => r.Company)
            .Include(r => r.TransportationType)
            .Include(r => r.CustomsProcedure)
            .Include(r => r.TheOtherCountry)
            .FirstAsync(r => r.Id == 300);

        _dbContext.WbrOrders.AddRange(
            new WbrOrder 
            { 
                Id = 301, 
                RegisterId = 300, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                Shk = "A",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrOrder 
            { 
                Id = 302, 
                RegisterId = 300, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                Shk = "A",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrOrder 
            { 
                Id = 303, 
                RegisterId = 300, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                Shk = "B",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            }
        );
        await _dbContext.SaveChangesAsync();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(300);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(2));
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var xmlContent = reader.ReadToEnd();
            var doc = XDocument.Parse(xmlContent);
            Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        }
    }

    [Test]
    public async Task GenerateXML4R_OzonOrder_IndPostApi_CreatesZipWithUniqueOrders()
    {
        // Fetch required related entities from the context
        var countryRu = await _dbContext.Countries.FirstAsync(c => c.IsoNumeric == 643);
        var countryUz = await _dbContext.Countries.FirstAsync(c => c.IsoNumeric == 860);
        var company = await _dbContext.Companies.FirstAsync(c => c.Id == 1);
        var transportationType = await _dbContext.TransportationTypes.FirstAsync(t => t.Id == 1);
        var customsProcedure = await _dbContext.CustomsProcedures.FirstAsync(c => c.Id == 1);

        // Ensure company navigation property is set
        company.Country = countryRu;
        _dbContext.Companies.Update(company);
        await _dbContext.SaveChangesAsync();

        var register = new Register {
            Id = 400,
            CompanyId = company.Id,
            Company = company,
            TransportationTypeId = transportationType.Id,
            TransportationType = transportationType,
            CustomsProcedureId = customsProcedure.Id,
            CustomsProcedure = customsProcedure,
            FileName = "ozon_zip.xlsx",
            DTime = DateTime.Now,
            TheOtherCountryCode = countryUz.IsoNumeric,
            TheOtherCountry = countryUz
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var loadedRegister = await _dbContext.Registers
            .Include(r => r.Company)
            .Include(r => r.TransportationType)
            .Include(r => r.CustomsProcedure)
            .Include(r => r.TheOtherCountry)
            .FirstAsync(r => r.Id == 400);

        _dbContext.OzonOrders.AddRange(
            new OzonOrder { 
                Id = 401, 
                RegisterId = 400, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                PostingNumber = "X",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonOrder 
            { 
                Id = 402, 
                RegisterId = 400, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                PostingNumber = "X",
                CheckStatusId = (int)ParcelCheckStatusCode.Approved
            },
            new OzonOrder 
            { 
                Id = 403, 
                RegisterId = 400, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                PostingNumber = "Y",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            }
        );
        await _dbContext.SaveChangesAsync();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(400);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(2));
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var xmlContent = reader.ReadToEnd();
            var doc = XDocument.Parse(xmlContent);
            Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        }
    }

    [Test]
    public async Task GenerateXML4R_OtherCompany_ReturnsEmptyZip()
    {
        // Create a register with CompanyId = 3 (neither Ozon nor WBR)
        var otherCompany = await _dbContext.Companies.FirstAsync(c => c.Id == 3);
        var register = new Register {
            Id = 700,
            CompanyId = otherCompany.Id,
            Company = otherCompany,
            FileName = "other_company.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "OTHER-001"
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Create some generic orders for this register (they won't be included in the ZIP)
        var dummyOrder = new DummyOrder { 
            Id = 701, 
            RegisterId = 700, 
            Register = register,
            StatusId = 1
        };
        _dbContext.Orders.Add(dummyOrder);
        await _dbContext.SaveChangesAsync();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(700);

        // Verify the file is named correctly based on the register's invoice number
        Assert.That(fileName, Is.EqualTo($"IndPost_{register.InvoiceNumber}.zip"));

        // Verify the ZIP file exists but is empty (no entries)
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(0));
    }

    [Test]
    public void GenerateXML_ThrowsException_WhenOrderNotFound()
    {
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.GenerateXML(-999));
        Assert.That(ex!.Message, Does.Contain("Order not found"));
    }

    [Test]
    public void GenerateXML_ThrowsException_WhenOrderMarkedByPartner()
    {
        var register = new Register
        {
            Id = 20,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "marked.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-MARKED",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };

        _dbContext.Registers.Add(register);

        var order = new WbrOrder
        {
            Id = 21,
            RegisterId = 20,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner
        };

        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.GenerateXML(21));
        Assert.That(ex!.Message, Does.Contain("Order is not eligible for IndPost XML"));
    }

    [Test]
    public async Task GenerateXML4R_ReturnsEmptyZip_WhenNoOrders()
    {
        var register = new Register {
            Id = 500,
            CompanyId = 1,
            FileName = "empty.xlsx",
            DTime = DateTime.Now
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(500);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GenerateXML4R_OnlyUniqueOrdersIncluded()
    {
        var register = new Register {
            Id = 600,
            CompanyId = 1,
            FileName = "dupes.xlsx",
            DTime = DateTime.Now
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();
        var loadedRegister = await _dbContext.Registers.FirstAsync(r => r.Id == 600);
        _dbContext.OzonOrders.AddRange(
            new OzonOrder 
            { 
                Id = 601, 
                RegisterId = 600, 
                Register = loadedRegister, 
                StatusId = 1, 
                PostingNumber = "Z",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonOrder 
            { 
                Id = 602, 
                RegisterId = 600, 
                Register = loadedRegister, 
                StatusId = 1, 
                PostingNumber = "Z",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonOrder 
            { 
                Id = 603, 
                RegisterId = 600, 
                Register = loadedRegister, 
                StatusId = 1, 
                PostingNumber = "W",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            }
        );
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(600);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task GenerateXML4R_SkipsOrdersMarkedByPartner()
    {
        var register = new Register
        {
            Id = 800,
            CompanyId = 1,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "skip_marked.xlsx",
            DTime = DateTime.Now,
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var loadedRegister = await _dbContext.Registers
            .Include(r => r.Company)
            .Include(r => r.TransportationType)
            .Include(r => r.CustomsProcedure)
            .Include(r => r.TheOtherCountry)
            .FirstAsync(r => r.Id == 800);

        _dbContext.OzonOrders.AddRange(
            new OzonOrder { 
                Id = 801, 
                RegisterId = 800, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = 643, 
                PostingNumber = "PN1", 
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonOrder 
            { 
                Id = 802, 
                RegisterId = 800, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = 643, 
                PostingNumber = "PN2", 
                CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner }
        );
        await _dbContext.SaveChangesAsync();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(800);

        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(1));
        Assert.That(archive.Entries[0].Name, Does.Contain("PN1"));
    }

    [Test]
    public async Task GenerateXML_ThrowsException_WhenOrderCheckStatusIsBelowNoIssues()
    {
        var register = new Register
        {
            Id = 30,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "below_noissues.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-BELOW",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        var order = new WbrOrder
        {
            Id = 31,
            RegisterId = 30,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Below NoIssues
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.GenerateXML(31));
        Assert.That(ex!.Message, Does.Contain("not eligible for IndPost XML"));
    }

    [Test]
    public async Task GenerateXML4R_SkipsOrdersWithCheckStatusBelowNoIssues()
    {
        var register = new Register
        {
            Id = 32,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "skip_below_noissues.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-SKIP-BELOW",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        var order1 = new WbrOrder
        {
            Id = 33,
            RegisterId = 32,
            StatusId = 1,
            CountryCode = 643,
            Shk = "A",
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Should be skipped
        };
        var order2 = new WbrOrder
        {
            Id = 34,
            RegisterId = 32,
            StatusId = 1,
            CountryCode = 643,
            Shk = "B",
            CheckStatusId = (int)ParcelCheckStatusCode.NotChecked // Should be included
        };
        _dbContext.WbrOrders.AddRange(order1, order2);
        _dbContext.SaveChanges();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(32);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(1));
        Assert.That(archive.Entries[0].Name, Does.Contain("B"));
    }

    [Test]
    public async Task GenerateXML_ThrowsException_WhenOrderCheckStatusIsInHasIssuesToNoIssuesRange()
    {
        var register = new Register
        {
            Id = 35,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "range_hasissues_noissues.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-RANGE",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        var order = new WbrOrder
        {
            Id = 36,
            RegisterId = 35,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // In range
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await svc.GenerateXML(36));
        Assert.That(ex!.Message, Does.Contain("not eligible for IndPost XML"));
    }

    [Test]
    public async Task GenerateXML4R_SkipsOrdersWithCheckStatusInHasIssuesToNoIssuesRange()
    {
        var register = new Register
        {
            Id = 37,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "skip_range_hasissues_noissues.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "INV-SKIP-RANGE",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);
        var order1 = new WbrOrder
        {
            Id = 38,
            RegisterId = 37,
            StatusId = 1,
            CountryCode = 643,
            Shk = "A",
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Should be skipped
        };
        var order2 = new WbrOrder
        {
            Id = 39,
            RegisterId = 37,
            StatusId = 1,
            CountryCode = 643,
            Shk = "B",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues // Should be included
        };
        _dbContext.WbrOrders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(37);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(1));
        Assert.That(archive.Entries[0].Name, Does.Contain("B"));
    }

    public class DummyOrder : BaseOrder {
        public override string GetParcelNumber() => "DUMMY";
        public override string GetCurrency() => "DUMMY";
        public override string GetDescription() => "DUMMY";
        public override string GetQuantity() => "1";
        public override string GetCost() => "0";
        public override string GetWeight() => "0";
        public override string GetUrl() => "";
        public override string GetCity() => "";
        public override string GetStreet() => "";
        public override string GetSurName() => "";
        public override string GetName() => "";
        public override string GetMiddleName() => "";
        public override string GetFullName() => "";
        public override string GetSeries() => "";
        public override string GetNumber() => "";
    }

}
