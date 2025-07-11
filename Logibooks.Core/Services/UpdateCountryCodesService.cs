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
using System.Net.Http;
using CsvHelper;
using CsvHelper.Configuration;
using Logibooks.Core.Data;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public class UpdateCountryCodesService(
    AppDbContext db,
    ILogger<UpdateCountryCodesService> logger,
    IHttpClientFactory httpClientFactory)
    : IUpdateCountryCodesService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<UpdateCountryCodesService> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private const string DataHubCountryCodesUrl = "https://datahub.io/core/country-codes/r/country-codes.csv";

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
        _logger.LogInformation("Downloading {Url}", DataHubCountryCodesUrl);

        byte[] data;
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            data = await httpClient.GetByteArrayAsync(DataHubCountryCodesUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download country codes from {Url}", DataHubCountryCodesUrl);
            throw;
        }

        using var reader = new StreamReader(new MemoryStream(data));
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null
        };
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<CsvRecord>().ToList();

        var existing = _db.Countries
            .ToDictionary(cc => cc.IsoNumeric);

        foreach (var record in records)
        {
            record.IsoAlpha2 = record.IsoAlpha2.ToUpperInvariant();
            if (existing.TryGetValue(record.IsoNumeric, out var countryCode))
            {
                MapRecordToCountryCode(record, countryCode);
                countryCode.IsoAlpha2 = record.IsoAlpha2;
                _db.Countries.Update(countryCode);
            }
            else
            {
                var newCountryCode = new Country
                {
                    IsoNumeric = record.IsoNumeric,
                    IsoAlpha2 = record.IsoAlpha2 
                };
                MapRecordToCountryCode(record, newCountryCode);
                _db.Countries.Add(newCountryCode);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Loaded {Count} country codes", records.Count);
    }

    private static void MapRecordToCountryCode(CsvRecord source, Country target)
    {
        target.NameEnShort = source.NameEnShort;
        target.NameEnFormal = source.NameEnFormal;
        target.NameEnOfficial = source.NameEnOfficial;
        target.NameEnCldr = source.NameEnCldr;
        target.NameRuShort = source.NameRuShort;
        target.NameRuFormal = source.NameRuFormal;
        target.NameRuOfficial = source.NameRuOfficial;
        target.LoadedAt = DateTime.UtcNow;
    }
}
