// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Interfaces;

public interface IIndPostXmlService
{
    string CreateXml(IDictionary<string, string?> fields, IEnumerable<IDictionary<string, string?>> goodsItems);
}
