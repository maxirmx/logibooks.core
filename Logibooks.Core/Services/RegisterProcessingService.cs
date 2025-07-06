using System.Globalization;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Settings;

namespace Logibooks.Core.Services;

public enum ProcessExcelError
{
    None,
    MappingNotFound,
    EmptyExcel
}

public class ProcessExcelResult
{
    public ProcessExcelError Error { get; init; }
    public string? MappingPath { get; init; }
    public Reference? Reference { get; init; }
}

public class RegisterProcessingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RegisterProcessingService> _logger;
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    public RegisterProcessingService(AppDbContext db, ILogger<RegisterProcessingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProcessExcelResult> ProcessExcelAsync(byte[] content, string fileName, string mappingFile = "register_mapping.yaml")
    {
        _logger.LogDebug("ProcessExcel for {file} ({size} bytes)", fileName, content.Length);

        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", mappingFile);
        if (!System.IO.File.Exists(mappingPath))
        {
            _logger.LogError("Mapping file not found at {path}", mappingPath);
            return new ProcessExcelResult { Error = ProcessExcelError.MappingNotFound, MappingPath = mappingPath };
        }

        var mapping = RegisterMapping.Load(mappingPath);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var dataSet = reader.AsDataSet();
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count <= 1)
        {
            _logger.LogDebug("ProcessExcel found empty Excel file");
            return new ProcessExcelResult { Error = ProcessExcelError.EmptyExcel };
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

        var register = new Register { FileName = fileName };
        _db.Registers.Add(register);
        await _db.SaveChangesAsync();

        var orders = new List<Order>();
        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var order = new Order { RegisterId = register.Id, StatusId = 1 };
            foreach (var kv in columnMap)
            {
                var val = row[kv.Key]?.ToString();
                var propInfo = typeof(Order).GetProperty(kv.Value);
                if (propInfo != null && val != null)
                {
                    try
                    {
                        object? convertedValue = ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name);
                        propInfo.SetValue(order, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}", val, propInfo.Name);
                    }
                }
            }
            orders.Add(order);
        }

        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        _logger.LogDebug("ProcessExcel imported {count} orders", orders.Count);
        return new ProcessExcelResult { Error = ProcessExcelError.None, Reference = new Reference { Id = register.Id } };
    }

    public object? ConvertValueToPropertyType(string? value, Type propertyType, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (propertyType.IsClass || Nullable.GetUnderlyingType(propertyType) != null)
                return propertyType == typeof(string) ? string.Empty : null;
        }

        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType == typeof(int) || targetType == typeof(Int32))
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
            if (DateTime.TryParse(value, RussianCulture, DateTimeStyles.None, out DateTime dateTimeResult))
                return DateOnly.FromDateTime(dateTimeResult);
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
}
