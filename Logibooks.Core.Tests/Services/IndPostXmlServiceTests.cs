using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class IndPostXmlServiceTests
{
    [Test]
    public void CreateXml_GeneratesXmlWithGoods()
    {
        var svc = new IndPostXmlService();
        var fields = new Dictionary<string, string?> { ["NUM"] = "1" };
        var goods = new List<Dictionary<string, string?>>
        {
            new() { ["DESCR"] = "Item1", ["QTY"] = "10" }
        };

        var xml = svc.CreateXml(fields, goods);
        var doc = XDocument.Parse(xml);

        Assert.That(doc.Root?.Name.LocalName, Is.EqualTo("AltaIndPost"));
        Assert.That(doc.Root?.Element("NUM")?.Value, Is.EqualTo("1"));
        var goodsEls = doc.Root?.Elements("GOODS").ToList();
        Assert.That(goodsEls?.Count, Is.EqualTo(1));
        Assert.That(goodsEls?[0].Element("DESCR")?.Value, Is.EqualTo("Item1"));
    }

    [Test]
    public void CreateXml_AllowsMultipleGoods()
    {
        var svc = new IndPostXmlService();
        var goods = new List<Dictionary<string, string?>>
        {
            new() { ["DESCR"] = "Item1" },
            new() { ["DESCR"] = "Item2" }
        };

        var xml = svc.CreateXml(new Dictionary<string, string?>(), goods);
        var doc = XDocument.Parse(xml);

        var descrs = doc.Root?.Elements("GOODS").Select(e => e.Element("DESCR")?.Value).ToList();
        Assert.That(descrs, Is.EqualTo(new[] { "Item1", "Item2" }));
    }
}
