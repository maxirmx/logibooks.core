using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Logibooks.Core.Services;

/// <summary>
/// Service for creating AltaIndPost XML documents based on the IndPost.xsd schema.
/// Only top level elements are supported. Extra elements in the input are skipped.
/// </summary>
public class IndPostXmlService : IIndPostXmlService
{
    private readonly XmlSchemaSet _schemas = new();
    private readonly string[] _allowedElements;

    public IndPostXmlService()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "mapping", "IndPost.xsd");
        using var fs = File.OpenRead(schemaPath);
        var schema = XmlSchema.Read(fs, null) ?? throw new InvalidOperationException("Failed to load XSD");
        _schemas.Add(schema);

        // collect allowed direct child element names of AltaIndPost
        _allowedElements = schema.Items
            .OfType<XmlSchemaElement>()
            .Where(e => e.Name == "AltaIndPost")
            .SelectMany(e => ((XmlSchemaComplexType)e.SchemaType!).ContentTypeParticle is XmlSchemaSequence seq ? seq.Items.OfType<XmlSchemaElement>().Select(el => el.Name) : [])
            .ToArray();
    }

    public XmlDocument Generate(Dictionary<string, string?> values)
    {
        var doc = new XmlDocument();
        doc.Schemas.Add(_schemas);

        var root = doc.CreateElement("AltaIndPost");
        doc.AppendChild(root);

        foreach (var kv in values)
        {
            if (!_allowedElements.Contains(kv.Key))
                continue;

            var elem = doc.CreateElement(kv.Key);
            if (kv.Value != null)
            {
                elem.InnerText = kv.Value;
            }
            root.AppendChild(elem);
        }

        // validate document
        doc.Validate(null);
        return doc;
    }
}
