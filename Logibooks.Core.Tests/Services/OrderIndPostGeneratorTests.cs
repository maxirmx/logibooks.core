using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class OrderIndPostGeneratorTests
{
    [Test]
    public async Task GenerateFilename_WbrOrder_PadsShk()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wbr_{System.Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        var order = new WbrOrder { Id = 1, RegisterId = 1, StatusId = 1, Shk = "123" };
        db.Orders.Add(order);
        db.SaveChanges();

        var svc = new OrderIndPostGenerator(db, new IndPostXmlService());
        var name = await svc.GenerateFilename(1);
        Assert.That(name, Is.EqualTo("IndPost_00000000000000000123.xml"));
    }

    [Test]
    public async Task GenerateFilename_OzonOrder_UsesOzonId()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ozon_{System.Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        var order = new OzonOrder { Id = 2, RegisterId = 1, StatusId = 1, OzonId = "OZ-1" };
        db.Orders.Add(order);
        db.SaveChanges();

        var svc = new OrderIndPostGenerator(db, new IndPostXmlService());
        var name = await svc.GenerateFilename(2);
        Assert.That(name, Is.EqualTo("IndPost_OZ-1.xml"));
    }

    [Test]
    public async Task GenerateXML_ReturnsXml()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"xml_{System.Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        var order = new WbrOrder { Id = 3, RegisterId = 1, StatusId = 1 };
        db.Orders.Add(order);
        db.SaveChanges();

        var svc = new OrderIndPostGenerator(db, new IndPostXmlService());
        var xml = await svc.GenerateXML(3);
        var doc = XDocument.Parse(xml);
        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
    }
}
