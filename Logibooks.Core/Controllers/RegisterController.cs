using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Authorization;

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
        var items = Enumerable.Range(1, 50)
            .Select(i => new RegisterItem { Id = i, Date = DateTime.Today.AddDays(-i), FileName = $"register_{i}.xlsx" })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Ok(items);
    }

    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DownloadRegister(string? format = "xlsx")
    {
        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            return _403();
        }

        // Define supported formats
        var supportedFormats = new[] { "xlsx", "zip" };

        // Validate the format
        if (!string.IsNullOrEmpty(format) && !supportedFormats.Contains(format.ToLower()))
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                new ErrMessage { Msg = $"Unsupported format '{format}'. Supported formats are: {string.Join(", ", supportedFormats)}" });
        }

        byte[] content = System.Text.Encoding.UTF8.GetBytes("sample register");
        string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        string fileName = "register.xlsx";

        if (string.Equals(format, "zip", StringComparison.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var entry = zip.CreateEntry("register.xlsx");
                using var es = entry.Open();
                es.Write(content, 0, content.Length);
            }
            content = ms.ToArray();
            contentType = "application/zip";
            fileName = "register.zip";
        }

        return File(content, contentType, fileName);
    }
}
