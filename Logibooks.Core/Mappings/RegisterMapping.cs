using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Logibooks.Core.Mappings;

public class RegisterMapping
{
    public Dictionary<string, string> HeaderMappings { get; set; } = new();

    public static RegisterMapping Load(string path)
    {
        using var reader = new StreamReader(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<RegisterMapping>(reader) ?? new RegisterMapping();
    }
}
