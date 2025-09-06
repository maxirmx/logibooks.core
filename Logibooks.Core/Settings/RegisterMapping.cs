// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Logibooks.Core.Settings;
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
