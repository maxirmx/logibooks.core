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
public class OrderIndPostGeneratorTests
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
            DestCountryCode = 860 // Uzbekistan
        };

        _dbContext.Registers.Add(register);

        var order = new WbrOrder { Id = 3, RegisterId = 10, StatusId = 1, CountryCode = 643 };

        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        var svc = new OrderIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(3);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
    }

/*    [Test]
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
            DestCountryCode = 860
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
            TnVed = "12345678"
        };
        _dbContext.WbrOrders.Add(order);
        _dbContext.SaveChanges();
        var svc = new OrderIndPostGenerator(_dbContext, new IndPostXmlService());
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
            DestCountryCode = 643
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
            TnVed = "87654321"
        };
        _dbContext.OzonOrders.Add(order);
        _dbContext.SaveChanges();
        var svc = new OrderIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(201);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        Assert.That(filename, Does.StartWith("IndPost_"));
        Assert.That(xml, Does.Contain("Ozon Product"));
    }

    [Test]
    public async Task GenerateXML4R_WbrOrder_IndPostApi_CreatesZipWithUniqueOrders()
    {
        var register = new Register { Id = 300, CompanyId = 2, TransportationTypeId = 1, CustomsProcedureId = 1, FileName = "wbr_zip.xlsx" };
        _dbContext.Registers.Add(register);
        _dbContext.WbrOrders.AddRange(
            new WbrOrder { Id = 301, RegisterId = 300, StatusId = 1, CountryCode = 643, Shk = "A" },
            new WbrOrder { Id = 302, RegisterId = 300, StatusId = 1, CountryCode = 643, Shk = "A" },
            new WbrOrder { Id = 303, RegisterId = 300, StatusId = 1, CountryCode = 643, Shk = "B" }
        );
        _dbContext.SaveChanges();
        var svc = new OrderIndPostGenerator(_dbContext, new IndPostXmlService());
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
        var register = new Register { Id = 400, CompanyId = 1, TransportationTypeId = 1, CustomsProcedureId = 1, FileName = "ozon_zip.xlsx" };
        _dbContext.Registers.Add(register);
        _dbContext.OzonOrders.AddRange(
            new OzonOrder { Id = 401, RegisterId = 400, StatusId = 1, CountryCode = 643, PostingNumber = "P1" },
            new OzonOrder { Id = 402, RegisterId = 400, StatusId = 1, CountryCode = 643, PostingNumber = "P1" },
            new OzonOrder { Id = 403, RegisterId = 400, StatusId = 1, CountryCode = 643, PostingNumber = "P2" }
        );
        _dbContext.SaveChanges();
        var svc = new OrderIndPostGenerator(_dbContext, new IndPostXmlService());
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
*/
}
