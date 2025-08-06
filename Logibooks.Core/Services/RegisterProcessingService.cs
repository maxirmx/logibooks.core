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

    private Dictionary<string, short>? _wbrCountryLookup;
    private Dictionary<string, short>? _ozonCountryLookup;

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

        await InitializeCountryLookupsAsync(isWbr, cancellationToken);

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

        // Use lookup tables to set TheOtherCountryCode to Uzbekistan if present
        short uzbekistanCode = 0;
        if (isWbr && _wbrCountryLookup != null && _wbrCountryLookup.TryGetValue("UZ", out var wbrCode) && wbrCode != 0)
        {
            uzbekistanCode = wbrCode;
        }
        else if (!isWbr && _ozonCountryLookup != null && _ozonCountryLookup.TryGetValue("Узбекистан", out var ozonCode) && ozonCode != 0)
        {
            uzbekistanCode = ozonCode;
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
            var orders = CreateWbrOrders(table, register.Id, columnMap);
            _db.Orders.AddRange(orders);
            count = orders.Count;
        }
        else // Ozon
        {
            var orders = CreateOzonOrders(table, register.Id, columnMap);
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

        List<BaseOrder> orders;
        if (isWbr)
        {
            orders = await _db.WbrOrders.AsNoTracking()
                .Where(o => o.RegisterId == registerId)
                .Cast<BaseOrder>()
                .ToListAsync(cancellationToken);
        }
        else
        {
            orders = await _db.OzonOrders.AsNoTracking()
                .Where(o => o.RegisterId == registerId)
                .Cast<BaseOrder>()
                .ToListAsync(cancellationToken);
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("register");

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
                if (val is DateOnly dOnly)
                {
                    cellValue = dOnly.ToString("yyyy-MM-dd", RussianCulture);
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

            if (baseOrder.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues &&
                baseOrder.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues)
            {
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.Red;
            }

            row++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task InitializeCountryLookupsAsync(bool isWbr, CancellationToken cancellationToken = default)
    {
        var countries = await _db.Countries.AsNoTracking().ToListAsync(cancellationToken);
        if (isWbr)
        {
            if (_wbrCountryLookup == null)
            {
                var wbrGroups = countries
                    .Where(c => !string.IsNullOrWhiteSpace(c.IsoAlpha2))
                    .GroupBy(c => c.IsoAlpha2.ToUpperInvariant())
                    .ToList();
                foreach (var group in wbrGroups)
                {
                    if (group.Count() > 1)
                    {
                        _logger.LogWarning("Duplicate WBR country code detected: {Code}. Using first occurrence.", group.Key);
                    }
                }
                _wbrCountryLookup = wbrGroups
                    .ToDictionary(g => g.Key, g => g.First().IsoNumeric, StringComparer.OrdinalIgnoreCase);
            }
            // Clear Ozon lookup to save memory
            _ozonCountryLookup = null;
        }
        else
        {
            if (_ozonCountryLookup == null)
            {
                var ozonGroups = countries
                    .Where(c => !string.IsNullOrWhiteSpace(c.NameRuShort))
                    .GroupBy(c => c.NameRuShort)
                    .ToList();
                foreach (var group in ozonGroups)
                {
                    if (group.Count() > 1)
                    {
                        _logger.LogWarning("Duplicate Ozon country name detected: {Name}. Using first occurrence.", group.Key);
                    }
                }
                _ozonCountryLookup = ozonGroups
                    .ToDictionary(g => g.Key, g => g.First().IsoNumeric, StringComparer.InvariantCultureIgnoreCase);
                // Add special case for Russia if not already present
                _ozonCountryLookup["Россия"] = 643;
            }
            // Clear WBR lookup to save memory
            _wbrCountryLookup = null;
        }
    }

    private List<WbrOrder> CreateWbrOrders(System.Data.DataTable table, int registerId, Dictionary<int, string> columnMap)
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
                        if (propInfo.Name == nameof(BaseOrder.CountryCode))
                        {
                            order.CountryCode = LookupWbrCountryCode(val);
                            if (order.CountryCode == 0)
                            { 
                                _logger.LogInformation("Skipping row [{r}] because country code {'code'} was not recognized", r, val);
                            }
                        }
                        else
                        {
                            object? convertedValue = ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name);
                            propInfo.SetValue(order, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}", val, propInfo.Name);
                    }
                }
            }
            if (order.CountryCode != 0) orders.Add(order);
        }

        return orders;
    }

    private List<OzonOrder> CreateOzonOrders(System.Data.DataTable table, int registerId, Dictionary<int, string> columnMap)
    {
        var orders = new List<OzonOrder>();
        var orderType = typeof(OzonOrder);

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

            var order = new OzonOrder { RegisterId = registerId, StatusId = 1, CheckStatusId = 1 };

            foreach (var kv in columnMap)
            {
                var val = row[kv.Key]?.ToString();
                var propInfo = orderType.GetProperty(kv.Value);
                if (propInfo != null && val != null)
                {
                    try
                    {
                        if (propInfo.Name == nameof(BaseOrder.CountryCode))
                        {
                            order.CountryCode = LookupOzonCountryCode(val);
                            if (order.CountryCode == 0)
                            {
                                _logger.LogInformation("Skipping row [{r}] because country code {'code'} was not recognized", r, val);
                            }
                        }
                        else
                        {
                            object? convertedValue = ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name);
                            propInfo.SetValue(order, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}", val, propInfo.Name);
                    }
                }
            }
            if (order.CountryCode != 0) orders.Add(order);
        }

        return orders;
    }

    private object? ConvertValueToPropertyType(string? value, Type propertyType, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (propertyType.IsClass || Nullable.GetUnderlyingType(propertyType) != null)
                return propertyType == typeof(string) ? string.Empty : null;
        }

        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType == typeof(int))
        {
            return int.TryParse(value, NumberStyles.Integer, RussianCulture, out int result) ? result : default;
        }
        else if (targetType == typeof(decimal))
        {
            string normalizedVal = value?.Replace('.', ',') ?? "0";
            return decimal.TryParse(normalizedVal, NumberStyles.AllowDecimalPoint, RussianCulture, out decimal result) ? result : default;
        }
        else if (targetType == typeof(double))
        {
            string normalizedVal = value?.Replace('.', ',') ?? "0";
            return double.TryParse(normalizedVal, NumberStyles.AllowDecimalPoint, RussianCulture, out double result) ? result : default;
        }
        else if (targetType == typeof(bool))
        {
            if (string.IsNullOrWhiteSpace(value))
                return default(bool);

            string normalizedVal = value.ToLower(RussianCulture).Trim();
            string[] trueValues = ["1", "yes", "true", "да"];

            if (trueValues.Contains(normalizedVal, StringComparer.InvariantCultureIgnoreCase))
                return true;
            else
                return false;
        }
        else if (targetType == typeof(DateTime))
        {
            return DateTime.TryParse(value, RussianCulture, DateTimeStyles.None, out DateTime result) ? result : default;
        }
        else if (targetType == typeof(DateOnly))
        {
            if (DateOnly.TryParse(value, RussianCulture, DateTimeStyles.None, out DateOnly result))
                return result;
            return default(DateOnly);
        }
        else if (targetType == typeof(string))
        {
            return value ?? string.Empty;
        }

        try
        {
            return Convert.ChangeType(value, targetType, RussianCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not convert '{Value}' to type {Type} for property {Property}", value, targetType.Name, propertyName);
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }

    private short LookupWbrCountryCode(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        return _wbrCountryLookup?.GetValueOrDefault(code, (short)0) ?? 0;
    }

    private short LookupOzonCountryCode(string value)
    {
        var name = value.Trim();
        return _ozonCountryLookup?.GetValueOrDefault(name, (short)0) ?? 0;
    }
}
