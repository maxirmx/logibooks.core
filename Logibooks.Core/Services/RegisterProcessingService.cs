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

using ClosedXML.Excel;
using ExcelDataReader;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Settings;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Reflection;

namespace Logibooks.Core.Services;

public class RegisterProcessingService(AppDbContext db, ILogger<RegisterProcessingService> logger) : IRegisterProcessingService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<RegisterProcessingService> _logger = logger;
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    private Dictionary<string, short>? _countryLookup;

    public int GetOzonId() => IRegisterProcessingService.GetOzonId();
    public int GetWBRId() => IRegisterProcessingService.GetWBRId();

    public async Task<Reference> UploadRegisterFromExcelAsync(
        int companyId,
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        bool isWbr = companyId == 2; // WBR company ID
        string methodName = isWbr ? "UploadWbrRegisterFromExcelAsync" : "UploadOzonRegisterFromExcelAsync";
        string mappingFile = isWbr ? "wbr_register_mapping.yaml" : "ozon_register_mapping.yaml";

        await InitializeCountryLookupAsync(cancellationToken);

        _logger.LogDebug("{MethodName} for {file} ({size} bytes)", methodName, fileName, content.Length);

        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", mappingFile);
        if (!System.IO.File.Exists(mappingPath))
        {
            throw new FileNotFoundException("Mapping file not found", mappingPath);
        }

        var mapping = RegisterMapping.Load(mappingPath);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var dataSet = reader.AsDataSet();
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count <= 1)
        {
            throw new InvalidOperationException("Excel file is empty");
        }

        var table = dataSet.Tables[0];
        using var colorStream = new MemoryStream(content);
        using var colorWorkbook = new XLWorkbook(colorStream);
        var worksheet = colorWorkbook.Worksheet(1);
        var headerRow = table.Rows[0];
        var columnMap = new Dictionary<int, string>();
        for (int c = 0; c < table.Columns.Count; c++)
        {
            var header = headerRow[c]?.ToString() ?? string.Empty;
            if (mapping.HeaderMappings.TryGetValue(header, out var prop))
            {
                columnMap[c] = prop;
            }
        }

        // Use lookup table to set TheOtherCountryCode to Uzbekistan if present
        short uzbekistanCode = LookupCountryCode("UZ");
        if (uzbekistanCode == 0)
        {
            uzbekistanCode = LookupCountryCode("Узбекистан");
        }

        var register = new Register { FileName = fileName, CompanyId = companyId };
        if (uzbekistanCode != 0)
        {
            register.TheOtherCountryCode = uzbekistanCode;
        }
        _db.Registers.Add(register);
        await _db.SaveChangesAsync(cancellationToken);

        int count = 0;
        // Create orders based on company type
        if (isWbr)
        {
            var orders = CreateWbrOrders(table, register.Id, columnMap, worksheet);
            _db.Orders.AddRange(orders);
            count = orders.Count;
        }
        else // Ozon
        {
            var orders = CreateOzonOrders(table, register.Id, columnMap, worksheet);
            _db.Orders.AddRange(orders);
            count = orders.Count;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("{MethodName} imported {count} orders", methodName, count);
        return new Reference { Id = register.Id };
    }

    public async Task<byte[]> DownloadRegisterToExcelAsync(
        int registerId,
        CancellationToken cancellationToken = default)
    {
        var register = await _db.Registers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registerId, cancellationToken);
        if (register == null)
            throw new InvalidOperationException($"Register {registerId} not found");

        bool isWbr = register.CompanyId == GetWBRId();
        string mappingFile = isWbr ? "wbr_register_mapping.yaml" : "ozon_register_mapping.yaml";
        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", mappingFile);
        if (!File.Exists(mappingPath))
        {
            throw new FileNotFoundException("Mapping file not found", mappingPath);
        }
        var mapping = RegisterMapping.Load(mappingPath);
        var headers = mapping.HeaderMappings.Keys.ToList();
        var propMap = mapping.HeaderMappings.ToDictionary(k => k.Key, v => v.Value);

        // Build country code lookup
        var countryAlpha2Lookup = (await _db.Countries.AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(c => c.IsoNumeric, c => c.IsoAlpha2 ?? string.Empty);

        List<BaseParcel> orders;
        if (isWbr)
        {
            orders = await _db.WbrOrders.AsNoTracking()
                .Where(o => o.RegisterId == registerId)
                .OrderBy(o => o.Id)
                .Cast<BaseParcel>()
                .ToListAsync(cancellationToken);
        }
        else
        {
            orders = await _db.OzonOrders.AsNoTracking()
                .Where(o => o.RegisterId == registerId)
                .OrderBy(o => o.Id)
                .Cast<BaseParcel>()
                .ToListAsync(cancellationToken);
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Реестр");

        for (int i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        int row = 2;
        foreach (var baseOrder in orders)
        {
            var orderType = baseOrder.GetType();
            var propertyCache = new Dictionary<string, PropertyInfo>();

            for (int c = 0; c < headers.Count; c++)
            {
                var propName = propMap[headers[c]];
                if (!propertyCache.TryGetValue(propName, out var prop))
                {
                    prop = orderType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        propertyCache[propName] = prop;
                    }
                }
                object? val = prop?.GetValue(baseOrder);
                string cellValue = string.Empty;
                if (propName == nameof(BaseParcel.CountryCode) && val is short countryNumeric)
                {
                    cellValue = countryAlpha2Lookup.TryGetValue(countryNumeric, out var alpha2) ? alpha2 : countryNumeric.ToString();
                }
                else if (val is DateOnly dOnly)
                {
                    cellValue = dOnly.ToString("dd.MM.yyyy", RussianCulture);
                }
                else if (val is DateTime dt)
                {
                    cellValue = dt.ToString(RussianCulture);
                }
                else if (val != null)
                {
                    cellValue = Convert.ToString(val, RussianCulture) ?? string.Empty;
                }
                ws.Cell(row, c + 1).Value = cellValue;
            }

            if (baseOrder.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
            {
                if (baseOrder.PartnerColor != 0)
                {
                    ws.Row(row).Style.Fill.BackgroundColor = baseOrder.PartnerColorXL;
                }
            }
            else if (baseOrder.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues &&
                     baseOrder.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues)
            {
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.Red;
            }

            row++;
        }

        var dataRange = ws.Range(1, 1, row - 1, headers.Count);
        dataRange.SetAutoFilter();
        ws.SheetView.FreezeRows(1);

        ws.Columns().AdjustToContents(5.0, 30.0);
        const double rowHeight = 15.0; // Height in points (1 row ≈ 15 points)
        ws.Rows().Height = rowHeight;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task InitializeCountryLookupAsync(CancellationToken cancellationToken = default)
    {
        if (_countryLookup == null)
        {
            var countries = await _db.Countries.AsNoTracking().ToListAsync(cancellationToken);
            _countryLookup = new Dictionary<string, short>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var country in countries)
            {
                // Add IsoAlpha2 lookup (case-insensitive)
                if (!string.IsNullOrWhiteSpace(country.IsoAlpha2))
                {
                    var alpha2Key = country.IsoAlpha2.ToUpperInvariant();
                    if (!_countryLookup.TryAdd(alpha2Key, country.IsoNumeric))
                    {
                        _logger.LogWarning("Duplicate IsoAlpha2 country code detected: {Code}. Using first occurrence.", alpha2Key);
                    }
                }

                // Add NameRuShort lookup (case-insensitive)
                if (!string.IsNullOrWhiteSpace(country.NameRuShort))
                {
                    if (!_countryLookup.TryAdd(country.NameRuShort, country.IsoNumeric))
                    {
                        _logger.LogWarning("Duplicate NameRuShort country name detected: {Name}. Using first occurrence.", country.NameRuShort);
                    }
                }

                // Add IsoNumeric lookup (as string)
                var numericKey = country.IsoNumeric.ToString();
                _countryLookup.TryAdd(numericKey, country.IsoNumeric);
            }

            // Add special case for Russia (may overwrite existing entry, which is intentional)
            _countryLookup["Россия"] = 643;
        }
    }

    private List<WbrOrder> CreateWbrOrders(System.Data.DataTable table, int registerId, Dictionary<int, string> columnMap, IXLWorksheet worksheet)
    {
        var orders = new List<WbrOrder>();
        var orderType = typeof(WbrOrder);

        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            
            // Check if all cells in this row are empty
            bool isRowEmpty = true;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                if (!string.IsNullOrWhiteSpace(row[c]?.ToString()))
                {
                    isRowEmpty = false;
                    break;
                }
            }
            
            // Skip this row if all cells are empty
            if (isRowEmpty)
            {
                _logger.LogInformation("Skipping empty row [{r}]", r);
                continue;
            }

            var order = new WbrOrder { RegisterId = registerId, StatusId = 1, CheckStatusId = 1 };

            foreach (var kv in columnMap)
            {
                var val = row[kv.Key]?.ToString();
                var propInfo = orderType.GetProperty(kv.Value);
                if (propInfo != null && val != null)
                {
                    try
                    {
                        if (propInfo.Name == nameof(BaseParcel.CountryCode))
                        {
                            order.CountryCode = LookupCountryCode(val);
                            if (order.CountryCode == 0)
                            { 
                                _logger.LogInformation("Skipping row [{r}] because country code {'code'} was not recognized", r, val);
                            }
                        }
                        else
                        {
                            object? convertedValue = ExcelDataConverter.ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name, _logger);
                            propInfo.SetValue(order, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}", val, propInfo.Name);
                    }
                }
            }

            var (hasColor, rowColor) = ExcelColorParser.GetRowColor(worksheet, r + 1, _logger);
            if (hasColor)
            {
                order.CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner;
                if (rowColor is not null)
                {
                    order.PartnerColorXL = rowColor;
                }
            }

            if (order.CountryCode != 0) orders.Add(order);
        }

        return orders;
    }

    private List<OzonParcel> CreateOzonOrders(System.Data.DataTable table, int registerId, Dictionary<int, string> columnMap, IXLWorksheet worksheet)
    {
        var orders = new List<OzonParcel>();
        var orderType = typeof(OzonParcel);

        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            
            // Check if all cells in this row are empty
            bool isRowEmpty = true;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                if (!string.IsNullOrWhiteSpace(row[c]?.ToString()))
                {
                    isRowEmpty = false;
                    break;
                }
            }
            
            // Skip this row if all cells are empty
            if (isRowEmpty)
            {
                _logger.LogInformation("Skipping empty row [{r}]", r);
                continue;
            }

            var order = new OzonParcel { RegisterId = registerId, StatusId = 1, CheckStatusId = 1 };

            foreach (var kv in columnMap)
            {
                var val = row[kv.Key]?.ToString();
                var propInfo = orderType.GetProperty(kv.Value);
                if (propInfo != null && val != null)
                {
                    try
                    {
                        if (propInfo.Name == nameof(BaseParcel.CountryCode))
                        {
                            order.CountryCode = LookupCountryCode(val);
                            if (order.CountryCode == 0)
                            {
                                _logger.LogInformation("Skipping row [{r}] because country code {'code'} was not recognized", r, val);
                            }
                        }
                        else
                        {
                            object? convertedValue = ExcelDataConverter.ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name, _logger);
                            propInfo.SetValue(order, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}", val, propInfo.Name);
                    }
                }
            }

            var (hasColor, rowColor) = ExcelColorParser.GetRowColor(worksheet, r + 1, _logger);
            if (hasColor)
            {
                order.CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner;
                if (rowColor is not null)
                {
                    order.PartnerColorXL = rowColor;
                }
            }

            if (order.CountryCode != 0) orders.Add(order);
        }

        return orders;
    }

    private short LookupCountryCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var trimmedValue = value.Trim();
        return _countryLookup?.GetValueOrDefault(trimmedValue, (short)0) ?? 0;
    }
}
