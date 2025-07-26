using System.Collections.Generic;

namespace Logibooks.Core.Services;

public interface IIndPostXmlService
{
    string CreateXml(IDictionary<string, string?> fields, IEnumerable<IDictionary<string, string?>> goodsItems);
}
