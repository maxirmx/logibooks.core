// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Globalization;
namespace Logibooks.Core.Services;

/// <summary>
/// Helper class for converting Excel cell values to strongly typed properties
/// </summary>
public static class ExcelDataConverter
{
    private static readonly CultureInfo RussianCulture = new("ru-RU");

    /// <summary>
    /// Converts a string value from Excel to the specified property type
    /// </summary>
    /// <param name="value">The string value to convert</param>
    /// <param name="propertyType">The target property type</param>
    /// <param name="propertyName">The property name for logging purposes</param>
    /// <param name="logger">Optional logger for warnings</param>
    /// <returns>The converted value or default if conversion fails</returns>
    public static object? ConvertValueToPropertyType(string? value, Type propertyType, string propertyName, ILogger? logger = null)
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
            logger?.LogWarning(ex, "Could not convert '{Value}' to type {Type} for property {Property}", value, targetType.Name, propertyName);
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}