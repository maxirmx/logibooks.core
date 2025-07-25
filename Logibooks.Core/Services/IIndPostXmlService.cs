using System.Xml;

namespace Logibooks.Core.Services;

public interface IIndPostXmlService
{
    /// <summary>
    /// Generates an AltaIndPost XML document using values provided in the dictionary.
    /// Keys correspond to element names under the root element.
    /// Unknown keys are ignored.
    /// </summary>
    /// <param name="values">Mapping from element name to value.</param>
    /// <returns>Created XML document.</returns>
    XmlDocument Generate(Dictionary<string, string?> values);
}
