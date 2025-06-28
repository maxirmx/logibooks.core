using Microsoft.AspNetCore.Mvc;
using SharpCompress.Archives;
using ExcelDataReader;

using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Authorization;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Mappings;
using Logibooks.Core.Models;
using System.Globalization;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RegistersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<RegistersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<RegisterItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<RegisterItem>>> GetRegisters(int page = 1, int pageSize = 10)
    {
        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            return _403();
        }

        // Retrieve registers from database
        var registers = await _db.Registers
            .AsNoTracking()
            .OrderByDescending(r => r.Id) 
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Map database entities to RegisterItem DTOs
        var items = registers.Select(r => new RegisterItem
        {
            Id = r.Id,
            FileName = r.FileName,
            Date = r.DTime
        }).ToList();

        return Ok(items);
    }

    private async Task<IActionResult> ProcessExcel(byte[] content, string fileName)
    {
        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", "register_mapping.yaml");
        if (!System.IO.File.Exists(mappingPath))
        {
            _logger.LogError("Mapping file not found at {path}", mappingPath);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrMessage { Msg = "Mapping file missing" });
        }

        var mapping = RegisterMapping.Load(mappingPath);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var dataSet = reader.AsDataSet();
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
            return StatusCode(StatusCodes.Status400BadRequest,
                new ErrMessage { Msg = "Excel file is empty" });

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
                if (propInfo != null)
                {
                    if (propInfo != null && val != null)
                    {
                        try
                        {
                            // Convert string value to appropriate property type
                            object? convertedValue = ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name);
                            propInfo.SetValue(order, convertedValue);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}",
                                val, propInfo.Name);
                            // Continue with next property rather than failing the whole import
                        }
                    }
                }
            }
            orders.Add(order);
        }

        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Excel file imported", fileName, fileSize = content.Length, rows = orders.Count });
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UploadRegister(IFormFile file)
    {
        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            return _403();
        }

        if (file == null || file.Length == 0)
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                new ErrMessage { Msg = "No file was uploaded" });
        }

        try
        {
            // Get the file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Handle based on file type
            if (fileExtension == ".xlsx" || fileExtension == ".xls")
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] excelContent = ms.ToArray();
                return await ProcessExcel(excelContent, file.FileName);
            }
            else if (fileExtension == ".zip" || fileExtension == ".rar")
            {
                // Archive file - need to extract Excel
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                byte[] excelContent = [];
                string excelFileName = String.Empty;

                // Extract content from archive
                using (var archive = ArchiveFactory.Open(ms))
                {
                    var excelEntry = archive.Entries.FirstOrDefault(entry =>
                        !entry.IsDirectory &&
                        entry.Key != null && 
                        (Path.GetExtension(entry.Key).Equals(".xlsx", StringComparison.InvariantCultureIgnoreCase) ||
                         Path.GetExtension(entry.Key).Equals(".xls", StringComparison.InvariantCultureIgnoreCase)));

                    if (excelEntry == null || excelEntry.Key == null)
                    {
                        return StatusCode(StatusCodes.Status400BadRequest,
                            new ErrMessage { Msg = "No Excel file found in the archive" });
                    }

                    excelFileName = excelEntry.Key;

                    // Extract the Excel file
                    using var entryStream = new MemoryStream();
                    excelEntry.WriteTo(entryStream);
                    excelContent = entryStream.ToArray();
                }

                return await ProcessExcel(excelContent, excelFileName);
            }
            else
            {
                return StatusCode(StatusCodes.Status400BadRequest,
                    new ErrMessage { Msg = $"Unsupported file type: {fileExtension}. Supported types are: .xlsx, .xls, .zip, .rar" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing uploaded file");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrMessage { Msg = $"Error processing file: {ex.Message}" });
        }
    }

    private static readonly CultureInfo RussianCulture = new("ru-RU");

    private object? ConvertValueToPropertyType(string? value, Type propertyType, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // For nullable types or reference types, return null
            if (propertyType.IsClass || Nullable.GetUnderlyingType(propertyType) != null)
                return propertyType == typeof(string) ? string.Empty : null;

            // For value types, this will throw an exception if we don't handle specific cases below
        }

        // Get the underlying type if it's nullable
        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // Handle common types with specific conversion logic
        if (targetType == typeof(int) || targetType == typeof(Int32))
        {
            return int.TryParse(value, NumberStyles.Integer, RussianCulture, out int result) ? result : default;
        }
        else if (targetType == typeof(decimal))
        {
            // Handle both comma and dot as decimal separators
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
            // Handle various representations of boolean values
            if (string.IsNullOrWhiteSpace(value))
                return default(bool);

            string normalizedVal = value.Trim();
            var trueValues = new[] { "1", "yes", "true", "да", "Да", "ДА" };

            Console.WriteLine($"Converting '{value}' to boolean for property '{propertyName}'");
            foreach (var trueValue in trueValues)
            {
                Console.WriteLine($"Checking against true value: '{trueValue}'");
            }

            if (trueValues.Contains(normalizedVal, StringComparer.InvariantCultureIgnoreCase))
                return true;
            else 
                return false;
        }
        else if (targetType == typeof(DateTime))
        {
            return DateTime.TryParse(value, RussianCulture, DateTimeStyles.None, out DateTime result) ? result : default;
        }
        else if (targetType == typeof(string))
        {
            // For string properties, return the value directly or an empty string for null/empty input
            return value ?? string.Empty;
        }

        // For other types, try using the default conversion
        try
        {
            return Convert.ChangeType(value, targetType, RussianCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not convert '{Value}' to type {Type} for property {Property}",
                value, targetType.Name, propertyName);
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}
