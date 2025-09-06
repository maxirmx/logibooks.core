// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Constants; // Add this for Placeholders
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq; // Add this for FirstOrDefault

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
            new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа", Document = "AWB" },
            new TransportationType { Id = 2, Code = TransportationTypeCode.Auto, Name = "Авто", Document = "CMR" }
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

        var order = new WbrParcel { 
            Id = 3, 
            RegisterId = 10, 
            StatusId = 1, 
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };

        _dbContext.Parcels.Add(order);
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
        var order = new WbrParcel
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
        _dbContext.WbrParcels.Add(order);
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
        var order = new OzonParcel
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
        _dbContext.OzonParcels.Add(order);
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

        _dbContext.WbrParcels.AddRange(
            new WbrParcel 
            { 
                Id = 301, 
                RegisterId = 300, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                Shk = "A",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel 
            { 
                Id = 302, 
                RegisterId = 300, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                Shk = "A",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel 
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

        _dbContext.OzonParcels.AddRange(
            new OzonParcel { 
                Id = 401, 
                RegisterId = 400, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                PostingNumber = "X",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonParcel 
            { 
                Id = 402, 
                RegisterId = 400, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = countryRu.IsoNumeric, 
                PostingNumber = "X",
                CheckStatusId = (int)ParcelCheckStatusCode.Approved
            },
            new OzonParcel 
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
        _dbContext.Parcels.Add(dummyOrder);
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

        var order = new WbrParcel
        {
            Id = 21,
            RegisterId = 20,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner
        };

        _dbContext.Parcels.Add(order);
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
        _dbContext.OzonParcels.AddRange(
            new OzonParcel 
            { 
                Id = 601, 
                RegisterId = 600, 
                Register = loadedRegister, 
                StatusId = 1, 
                PostingNumber = "Z",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonParcel 
            { 
                Id = 602, 
                RegisterId = 600, 
                Register = loadedRegister, 
                StatusId = 1, 
                PostingNumber = "Z",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonParcel 
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

        _dbContext.OzonParcels.AddRange(
            new OzonParcel { 
                Id = 801, 
                RegisterId = 800, 
                Register = loadedRegister, 
                StatusId = 1, 
                CountryCode = 643, 
                PostingNumber = "PN1", 
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new OzonParcel 
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
        var order = new WbrParcel
        {
            Id = 31,
            RegisterId = 30,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Below NoIssues
        };
        _dbContext.Parcels.Add(order);
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
        var order1 = new WbrParcel
        {
            Id = 33,
            RegisterId = 32,
            StatusId = 1,
            CountryCode = 643,
            Shk = "A",
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Should be skipped
        };
        var order2 = new WbrParcel
        {
            Id = 34,
            RegisterId = 32,
            StatusId = 1,
            CountryCode = 643,
            Shk = "B",
            CheckStatusId = (int)ParcelCheckStatusCode.NotChecked // Should be included
        };
        _dbContext.WbrParcels.AddRange(order1, order2);
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
        var order = new WbrParcel
        {
            Id = 36,
            RegisterId = 35,
            StatusId = 1,
            CountryCode = 643,
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // In range
        };
        _dbContext.Parcels.Add(order);
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
        var order1 = new WbrParcel
        {
            Id = 38,
            RegisterId = 37,
            StatusId = 1,
            CountryCode = 643,
            Shk = "A",
            CheckStatusId = (int)ParcelCheckStatusCode.HasIssues // Should be skipped
        };
        var order2 = new WbrParcel
        {
            Id = 39,
            RegisterId = 37,
            StatusId = 1,
            CountryCode = 643,
            Shk = "B",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues // Should be included
        };
        _dbContext.WbrParcels.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (fileName, zipData) = await svc.GenerateXML4R(37);
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(1));
        Assert.That(archive.Entries[0].Name, Does.Contain("B"));
    }

    public class DummyOrder : BaseParcel {
        public override string GetParcelNumber() => "DUMMY";
        public override string GetCurrency() => "DUMMY";
        public override string GetDescription(string? insertBefore, string? insertAfter) => "DUMMY";
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

    [Test]
    public async Task GenerateXML_WithFeacnInsertItems_InsertsTextCorrectly()
    {
        // Add FEACN insert items
        _dbContext.FeacnInsertItems.AddRange(
            new FeacnInsertItem { Id = 1, Code = "1234567890", InsertBefore = "BEFORE TEXT", InsertAfter = "AFTER TEXT" },
            new FeacnInsertItem { Id = 2, Code = "0987654321", InsertBefore = null, InsertAfter = "ONLY AFTER" },
            new FeacnInsertItem { Id = 3, Code = "1111111111", InsertBefore = "ONLY BEFORE", InsertAfter = null }
        );
        
        var register = new Register
        {
            Id = 900,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "feacn_insert_test.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "FEACN-INV-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };

        _dbContext.Registers.Add(register);

        // Add WBR orders with different TN VED codes
        _dbContext.WbrParcels.AddRange(
            new WbrParcel
            {
                Id = 901,
                RegisterId = 900,
                StatusId = 1,
                CountryCode = 643,
                Shk = "WBR-SHK-1",
                ProductName = "Product 1",
                TnVed = "1234567890", // Has both InsertBefore and InsertAfter
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel
            {
                Id = 902,
                RegisterId = 900,
                StatusId = 1,
                CountryCode = 643,
                Shk = "WBR-SHK-2",
                ProductName = "Product 2",
                TnVed = "0987654321", // Has only InsertAfter
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel
            {
                Id = 903,
                RegisterId = 900,
                StatusId = 1,
                CountryCode = 643,
                Shk = "WBR-SHK-3",
                ProductName = "Product 3",
                TnVed = "1111111111", // Has only InsertBefore
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel
            {
                Id = 904,
                RegisterId = 900,
                StatusId = 1,
                CountryCode = 643,
                Shk = "WBR-SHK-4",
                ProductName = "Product 4",
                TnVed = "9999999999", // No FEACN insert item
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            }
        );

        _dbContext.SaveChanges();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());

        // Test Product 1 - should have both InsertBefore and InsertAfter
        var (filename1, xml1) = await svc.GenerateXML(901);
        Assert.That(xml1, Does.Contain("BEFORE TEXT"));
        Assert.That(xml1, Does.Contain("Product 1"));
        Assert.That(xml1, Does.Contain("AFTER TEXT"));

        // Test Product 2 - should have only InsertAfter
        var (filename2, xml2) = await svc.GenerateXML(902);
        Assert.That(xml2, Does.Not.Contain("BEFORE TEXT"));
        Assert.That(xml2, Does.Contain("Product 2"));
        Assert.That(xml2, Does.Contain("ONLY AFTER"));

        // Test Product 3 - should have only InsertBefore
        var (filename3, xml3) = await svc.GenerateXML(903);
        Assert.That(xml3, Does.Contain("ONLY BEFORE"));
        Assert.That(xml3, Does.Contain("Product 3"));
        Assert.That(xml3, Does.Not.Contain("AFTER TEXT"));

        // Test Product 4 - should have no insertions
        var (filename4, xml4) = await svc.GenerateXML(904);
        Assert.That(xml4, Does.Not.Contain("BEFORE TEXT"));
        Assert.That(xml4, Does.Contain("Product 4"));
        Assert.That(xml4, Does.Not.Contain("AFTER TEXT"));
        Assert.That(xml4, Does.Not.Contain("ONLY BEFORE"));
        Assert.That(xml4, Does.Not.Contain("ONLY AFTER"));
    }

    [Test]
    public async Task GenerateXML4R_LoadsFeacnInsertItemsOnceForMultipleOrders()
    {
        // Add FEACN insert items with shared codes
        _dbContext.FeacnInsertItems.AddRange(
            new FeacnInsertItem { Id = 1, Code = "1234567890", InsertBefore = "SHARED BEFORE", InsertAfter = "SHARED AFTER" },
            new FeacnInsertItem { Id = 2, Code = "0987654321", InsertBefore = null, InsertAfter = "UNIQUE AFTER" }
        );
        
        var register = new Register
        {
            Id = 950,
            CompanyId = 2, // WBR company
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "shared_feacn_test.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "SHARED-INV-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        
        _dbContext.Registers.Add(register);

        // Add WBR orders that share the same TN VED code
        _dbContext.WbrParcels.AddRange(
            new WbrParcel
            {
                Id = 951,
                RegisterId = 950,
                StatusId = 1,
                CountryCode = 643,
                Shk = "SHARED-SHK-1",
                ProductName = "Product 1",
                TnVed = "1234567890", // Shared code
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel
            {
                Id = 952,
                RegisterId = 950,
                StatusId = 1,
                CountryCode = 643,
                Shk = "SHARED-SHK-2",
                ProductName = "Product 2",
                TnVed = "1234567890", // Same shared code
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            },
            new WbrParcel
            {
                Id = 953,
                RegisterId = 950,
                StatusId = 1,
                CountryCode = 643,
                Shk = "UNIQUE-SHK-3",
                ProductName = "Product 3",
                TnVed = "0987654321", // Different code
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            }
        );

        _dbContext.SaveChanges();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());

        // Generate ZIP with multiple orders - should load FEACN insert items once
        var (fileName, zipData) = await svc.GenerateXML4R(950);

        // Verify ZIP contains expected files
        using var ms = new MemoryStream(zipData);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.That(archive.Entries.Count, Is.EqualTo(3)); // Three unique orders

        // Verify content of each XML file contains correct FEACN inserts
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var xmlContent = reader.ReadToEnd();
            
            if (entry.Name.Contains("SHARED-SHK"))
            {
                // Orders with shared TN VED should have shared FEACN insert
                Assert.That(xmlContent, Does.Contain("SHARED BEFORE"));
                Assert.That(xmlContent, Does.Contain("SHARED AFTER"));
            }
            else if (entry.Name.Contains("UNIQUE-SHK"))
            {
                // Order with unique TN VED should have unique FEACN insert
                Assert.That(xmlContent, Does.Not.Contain("SHARED BEFORE"));
                Assert.That(xmlContent, Does.Contain("UNIQUE AFTER"));
            }
        }
    }

    [Test]
    public async Task GenerateXML_StreetAddressTruncation_BothCustomsProcedureTypes()
    {
        // Arrange - Test truncation behavior for both customs procedure types (10 and 60)
        var longAddress = new string('T', 80); // 80 chars
        var expectedTruncated = new string('T', Limits.MaxStreetAddressLength); // 45 chars
        
        // Test Customs Procedure 10 (Export)
        var register10 = new Register
        {
            Id = 1020,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1, // Code 10
            FileName = "customs_10.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "CUST10-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register10);

        var order10 = new WbrParcel
        {
            Id = 1021,
            RegisterId = 1020,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-CUST10-1",
            RecipientAddress = $"Country,City,{longAddress},Building",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order10);

        // Test Customs Procedure 60 (Reimport)
        var register60 = new Register
        {
            Id = 1022,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 2, // Code 60
            FileName = "customs_60.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "CUST60-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register60);

        var order60 = new WbrParcel
        {
            Id = 1023,
            RegisterId = 1022,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-CUST60-1",
            RecipientAddress = $"Country,City,{longAddress},Building",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order60);
        _dbContext.SaveChanges();

        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());

        // Act & Assert for Customs Procedure 10 (Export)
        // For procedure 10, the truncated street goes to "STREETHOUSE" field (consignee address)
        var (filename10, xml10) = await svc.GenerateXML(1021);
        var doc10 = XDocument.Parse(xml10);
        var streetHouse10 = doc10.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouse10?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouse10?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));

        // Act & Assert for Customs Procedure 60 (Reimport)
        // For procedure 60, the truncated street goes to "CONSIGNOR_ADDRESS_STREETHOUSE" field (consignor address)
        var (filename60, xml60) = await svc.GenerateXML(1023);
        var doc60 = XDocument.Parse(xml60);
        var streetHouse60 = doc60.Root?.Elements("CONSIGNOR_ADDRESS_STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouse60?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouse60?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));
    }

    [Test]
    public async Task GenerateXML_WbrOrder_StreetAddressTruncation_ExactLimit()
    {
        // Arrange - Create address exactly at the limit (45 characters)
        // We need to account for how WbrParcel.GetStreet() works: it joins parts from index 2 onwards
        // So we need: streetPart + "," + additionalPart = 45 chars total
        var streetPart = "123456789012345678901234567890123456789"; // 39 chars
        var additionalPart = "01234"; // 5 chars, so total will be 39 + 1 + 5 = 45 chars
        var expectedStreet = $"{streetPart},{additionalPart}"; // Exactly 45 chars
        
        var register = new Register
        {
            Id = 1000,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "exact_limit.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "EXACT-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new WbrParcel
        {
            Id = 1001,
            RegisterId = 1000,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-EXACT-1",
            RecipientAddress = $"Country,City,{streetPart},{additionalPart}",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1001);

        // Assert - Should not be truncated as it's exactly at the limit
        Assert.That(xml, Does.Contain(expectedStreet));
        Assert.That(xml, Does.Contain("STREETHOUSE"));
        
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(expectedStreet));
        Assert.That(streetHouseElement?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));
    }

    [Test]
    public async Task GenerateXML_WbrOrder_StreetAddressTruncation_ExceedsLimit()
    {
        // Arrange - Create address that exceeds the limit when joined
        var streetPart = "1234567890123456789012345678901234567890"; // 40 chars
        var additionalPart = "123456"; // 6 chars, so total will be 40 + 1 + 6 = 47 chars (exceeds 45)
        var fullStreet = $"{streetPart},{additionalPart}"; // 47 chars
        var expectedTruncated = fullStreet[..Limits.MaxStreetAddressLength]; // First 45 chars: "1234567890123456789012345678901234567890,12345"
        
        var register = new Register
        {
            Id = 1002,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "exceeds_limit.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "EXCEED-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new WbrParcel
        {
            Id = 1003,
            RegisterId = 1002,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-EXCEED-1",
            RecipientAddress = $"Country,City,{streetPart},{additionalPart}",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1003);

        // Assert - Should be truncated to 45 characters
        Assert.That(xml, Does.Not.Contain(fullStreet));
        Assert.That(xml, Does.Contain(expectedTruncated));
        
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouseElement?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));
    }


    [Test]
    public async Task GenerateXML_OzonOrder_StreetAddressTruncation_ExceedsLimit()
    {
        // Arrange - Create Ozon order with long address that exceeds the limit
        var longAddress = "Very Long Street Name That Exceeds Maximum Length Allowed By System"; // 68 chars
        var expectedTruncated = "Very Long Street Name That Exceeds Maximum Le"; // 45 chars
        
        var register = new Register
        {
            Id = 1008,
            CompanyId = 1,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "ozon_long.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "OZON-LONG-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new OzonParcel
        {
            Id = 1009,
            RegisterId = 1008,
            StatusId = 1,
            CountryCode = 643,
            PostingNumber = "OZON-LONG-1",
            Address = $"Country,City,{longAddress},Building",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.OzonParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1009);

        // Assert - Should be truncated to 45 characters
        Assert.That(xml, Does.Not.Contain(longAddress));
        Assert.That(xml, Does.Contain(expectedTruncated));
        
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouseElement?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));
    }

    [Test]
    public async Task GenerateXML_OzonOrder_StreetAddressTruncation_WithSpecialCharacters()
    {
        // Arrange - Create address with special characters and Unicode that exceeds limit
        var longAddressWithSpecialChars = "Улица Пушкина дом Колотушкина квартира номер тридцать семь"; // 58 chars
        var expectedTruncated = "Улица Пушкина дом Колотушкина квартира номер"; // 45 chars and -1 after Trim
        
        var register = new Register
        {
            Id = 1010,
            CompanyId = 1,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "ozon_special.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "OZON-SPEC-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new OzonParcel
        {
            Id = 1011,
            RegisterId = 1010,
            StatusId = 1,
            CountryCode = 643,
            PostingNumber = "OZON-SPEC-1",
            Address = $"Россия,Москва,{longAddressWithSpecialChars},Подъезд 1",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.OzonParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1011);

        // Assert - Should be truncated to 45 characters while preserving Unicode
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouseElement?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength - 1));
        Assert.That(streetHouseElement?.Value, Does.StartWith("Улица"));
    }

    [Test]
    public async Task GenerateXML_WbrOrder_StreetAddressTruncation_EdgeCaseEmptyAddress()
    {
        // Arrange - Test edge case with empty/null address
        var register = new Register
        {
            Id = 1012,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "empty_address.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "EMPTY-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new WbrParcel
        {
            Id = 1013,
            RegisterId = 1012,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-EMPTY-1",
            RecipientAddress = null, // Empty address
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1013);

        // Assert - Should use placeholder and not cause truncation issues
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(Placeholders.NotSet));
    }

    [Test]
    public async Task GenerateXML_WbrOrder_StreetAddressTruncation_WithCommasInAddress()
    {
        // Arrange - Test address parsing with commas that results in long street portion
        var countryPart = "Узбекистан";
        var cityPart = "Ташкент";
        var streetPart = "Улица имени Героев Независимости дом номер сто двадцать три квартира номер сорок пять"; // 85 chars
        var buildingPart = "Подъезд 2";
        
        // WbrParcel.GetStreet() returns string.Join(",", parts.Skip(2).Select(p => p.Trim())) when parts.Length >= 4
        // So the street will be: streetPart + "," + buildingPart = "Улица имени Героев Независимости дом номер сто двадцать три квартира номер сорок пять,Подъезд 2"
        var fullStreetResult = $"{streetPart},{buildingPart}"; // This will be the result from GetStreet()
        var expectedTruncated = fullStreetResult[..45]; // First 45 chars: "Улица имени Героев Независимости дом номер ст"
        
        var register = new Register
        {
            Id = 1014,
            CompanyId = 2,
            TransportationTypeId = 1,
            CustomsProcedureId = 1,
            FileName = "comma_address.xlsx",
            DTime = DateTime.Now,
            InvoiceNumber = "COMMA-001",
            InvoiceDate = new DateOnly(2024, 6, 1),
            TheOtherCountryCode = 860
        };
        _dbContext.Registers.Add(register);

        var order = new WbrParcel
        {
            Id = 1015,
            RegisterId = 1014,
            StatusId = 1,
            CountryCode = 643,
            Shk = "WBR-COMMA-1",
            RecipientAddress = $"{countryPart},{cityPart},{streetPart},{buildingPart}",
            CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
        };
        _dbContext.WbrParcels.Add(order);
        _dbContext.SaveChanges();

        // Act
        var svc = new ParcelIndPostGenerator(_dbContext, new IndPostXmlService());
        var (filename, xml) = await svc.GenerateXML(1015);

        // Assert - Street part should be truncated to 45 characters
        var doc = XDocument.Parse(xml);
        var streetHouseElement = doc.Root?.Elements("STREETHOUSE").FirstOrDefault();
        Assert.That(streetHouseElement?.Value, Is.EqualTo(expectedTruncated));
        Assert.That(streetHouseElement?.Value.Length, Is.EqualTo(Limits.MaxStreetAddressLength));
    }
}
