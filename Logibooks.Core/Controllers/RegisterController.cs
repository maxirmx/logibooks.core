using Microsoft.AspNetCore.Mvc;
using SharpCompress.Archives;
using ExcelDataReader;

using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Authorization;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Mappings;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RegisterController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<RegisterController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
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
                    propInfo.SetValue(order, val);
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
}
