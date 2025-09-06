// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;
public class CountryCompactDto
{
    public short IsoNumeric { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string NameEnOfficial { get; set; } = string.Empty;
    public string NameRuOfficial { get; set; } = string.Empty;

    public CountryCompactDto() {}
    public CountryCompactDto(Country cc)
    {
        IsoNumeric = cc.IsoNumeric;
        IsoAlpha2 = cc.IsoAlpha2;
        NameEnOfficial = cc.NameEnOfficial;
        NameRuOfficial = cc.NameRuOfficial;
    }
}
