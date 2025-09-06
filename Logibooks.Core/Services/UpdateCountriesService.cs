// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public class UpdateCountriesService(
    AppDbContext db,
    ILogger<UpdateCountriesService> logger,
    IHttpClientFactory httpClientFactory)
    : IUpdateCountriesService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<UpdateCountriesService> _logger = logger;
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
            _logger.LogError(ex, "Failed to download country info from {Url}", DataHubCountryCodesUrl);
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
