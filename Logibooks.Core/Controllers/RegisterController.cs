using Microsoft.AspNetCore.Mvc;
using SharpCompress.Archives;

using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Authorization;
using System.IO;
using Microsoft.EntityFrameworkCore;

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
                // Direct Excel file
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                // Process Excel file directly
                byte[] excelContent = ms.ToArray();
                // TODO: Process the Excel file content

                return Ok(new { message = "Excel file processed successfully", fileSize = excelContent.Length });
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

                // Process the extracted Excel file
                // TODO: Process the Excel file content

                return Ok(new
                {
                    message = "Excel file extracted and processed successfully",
                    fileName = excelFileName,
                    fileSize = excelContent.Length
                });
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
