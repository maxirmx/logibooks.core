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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Logibooks.Core.Data;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public class UpdateCountryCodesService(
    AppDbContext db,
    ILogger<UpdateCountryCodesService> logger,
    HttpClient? httpClient = null)
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<UpdateCountryCodesService> _logger = logger;
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    private const string DefaultUrl =
        "https://datahub.io/core/country-codes/r/country-codes.csv";

    private class CsvRecord
    {
        [CsvHelper.Configuration.Attributes.Name("ISO3166-1-numeric")]
        public short IsoNumeric { get; set; }

        [CsvHelper.Configuration.Attributes.Name("ISO3166-1-Alpha-2")]
        public string IsoAlpha2 { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("UNTERM English Short")]
        public string NameEnShort { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("UNTERM English Formal")]
        public string NameEnFormal { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("official_name_en")]
        public string NameEnOfficial { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("CLDR display name")]
        public string NameEnCldr { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("UNTERM Russian Short")]
        public string NameRuShort { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("UNTERM Russian Formal")]
        public string NameRuFormal { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("official_name_ru")]
        public string NameRuOfficial { get; set; } = string.Empty;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var url = Environment.GetEnvironmentVariable("FETCH_URL") ?? DefaultUrl;
        _logger.LogInformation("Downloading {Url}", url);

        var data = await _httpClient.GetByteArrayAsync(url, cancellationToken);

        using var reader = new StreamReader(new MemoryStream(data));
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        };
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<CsvRecord>().ToList();

        var existing = _db.CountryCodes
            .ToDictionary(cc => cc.IsoNumeric);

        foreach (var r in records)
        {
            r.IsoAlpha2 = r.IsoAlpha2.ToUpperInvariant();
            if (existing.TryGetValue(r.IsoNumeric, out var cc))
            {
                cc.IsoAlpha2 = r.IsoAlpha2;
                cc.NameEnShort = r.NameEnShort;
                cc.NameEnFormal = r.NameEnFormal;
                cc.NameEnOfficial = r.NameEnOfficial;
                cc.NameEnCldr = r.NameEnCldr;
                cc.NameRuShort = r.NameRuShort;
                cc.NameRuFormal = r.NameRuFormal;
                cc.NameRuOfficial = r.NameRuOfficial;
                cc.LoadedAt = DateTime.UtcNow;
                _db.CountryCodes.Update(cc);
            }
            else
            {
                var newCc = new CountryCode
                {
                    IsoNumeric = r.IsoNumeric,
                    IsoAlpha2 = r.IsoAlpha2,
                    NameEnShort = r.NameEnShort,
                    NameEnFormal = r.NameEnFormal,
                    NameEnOfficial = r.NameEnOfficial,
                    NameEnCldr = r.NameEnCldr,
                    NameRuShort = r.NameRuShort,
                    NameRuFormal = r.NameRuFormal,
                    NameRuOfficial = r.NameRuOfficial,
                    LoadedAt = DateTime.UtcNow
                };
                _db.CountryCodes.Add(newCc);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Loaded {Count} country codes", records.Count);
    }
}
