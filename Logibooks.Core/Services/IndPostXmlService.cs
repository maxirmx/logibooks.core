using System.Collections.Generic;
using System.Xml.Linq;

namespace Logibooks.Core.Services;

public class IndPostXmlService : IIndPostXmlService
{
    public string CreateXml(IDictionary<string, string?> fields, IEnumerable<IDictionary<string, string?>> goodsItems)
    {
        var root = new XElement("AltaIndPost");

        foreach (var pair in fields)
        {
            if (pair.Value != null)
            {
                root.Add(new XElement(pair.Key, pair.Value));
            }
        }

        foreach (var item in goodsItems)
        {
            var goods = new XElement("GOODS");
            foreach (var pair in item)
            {
                if (pair.Value != null)
                {
                    goods.Add(new XElement(pair.Key, pair.Value));
                }
            }
            root.Add(goods);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return doc.ToString();
    }
}
