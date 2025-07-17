// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;
public class CountryDto
{
    public short IsoNumeric { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string NameEnShort { get; set; } = string.Empty;
    public string NameEnFormal { get; set; } = string.Empty;
    public string NameEnOfficial { get; set; } = string.Empty;
    public string NameEnCldr { get; set; } = string.Empty;
    public string NameRuShort { get; set; } = string.Empty;
    public string NameRuFormal { get; set; } = string.Empty;
    public string NameRuOfficial { get; set; } = string.Empty;
    public DateTime LoadedAt { get; set; }

    public CountryDto() {}
    public CountryDto(Country cc)
    {
        IsoNumeric = cc.IsoNumeric;
        IsoAlpha2 = cc.IsoAlpha2;
        NameEnShort = cc.NameEnShort;
        NameEnFormal = cc.NameEnFormal;
        NameEnOfficial = cc.NameEnOfficial;
        NameEnCldr = cc.NameEnCldr;
        NameRuShort = cc.NameRuShort;
        NameRuFormal = cc.NameRuFormal;
        NameRuOfficial = cc.NameRuOfficial;
        LoadedAt = cc.LoadedAt;
    }
}
