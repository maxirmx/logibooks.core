using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
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
        _dbContext.Countries.Add(new Country {
            IsoNumeric = 643,
            IsoAlpha2 = "RU",
            NameRuShort = "���������� ���������"
        });
        _dbContext.Countries.Add(new Country {
            IsoNumeric = 860,
            IsoAlpha2 = "UZ",
            NameRuShort = "����������"
        });
        _dbContext.Companies.AddRange(
            new Company {
                Id = 1,
                Inn = "7704217370",
                Kpp = "997750001",
                Name = "��� \"�������� �������\"",
                ShortName = "",
                CountryIsoNumeric = 643,
                PostalCode = "123112",
                City = "������",
                Street = "����������� ���������� �.10, ���.1, ���� 41, ���.6"
            },
            new Company {
                Id = 2,
                Inn = "9714053621",
                Kpp = "507401001",
                Name = "",
                ShortName = "��� \"���\"",
                CountryIsoNumeric = 643,
                PostalCode = "",
                City = "�. ��������",
                Street = "�������������� ���� ��������, �.6, ���.1"
            }
        );
        _dbContext.TransportationTypes.AddRange(
            new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "����" },
            new TransportationType { Id = 2, Code = TransportationTypeCode.Auto, Name = "����" }
        );
        _dbContext.CustomsProcedures.AddRange(
            new CustomsProcedure { Id = 1, Code = 10, Name = "�������" },
            new CustomsProcedure { Id = 2, Code = 60, Name = "��������" }
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
}
