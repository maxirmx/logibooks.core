using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Authorization;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class AltaController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<AltaController> logger,
    HttpClient? httpClient = null) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly HttpClient? _httpClient = httpClient;
    // POST api/alta/parse
    [HttpPost("parse")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<int>> Parse()
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();

        var urls = new List<string>
        {
            "https://example.com/alta1",
            "https://example.com/alta2"
        };

        var (items, exceptions) = await AltaParser.ParseAsync(urls, _httpClient);
        if (items.Count != 0) _db.AltaItems.AddRange(items);
        if (exceptions.Count != 0) _db.AltaExceptions.AddRange(exceptions);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // CRUD for AltaItems
    [HttpGet("items")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AltaItemDto>))]
    public async Task<ActionResult<IEnumerable<AltaItemDto>>> GetItems()
    {
        var items = await _db.AltaItems.AsNoTracking().ToListAsync();
        return items.Select(i => new AltaItemDto(i)).ToList();
    }

    [HttpGet("items/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AltaItemDto))]
    public async Task<ActionResult<AltaItemDto>> GetItem(int id)
    {
        var item = await _db.AltaItems.FindAsync(id);
        return item == null ? _404Object(id) : new AltaItemDto(item);
    }

    [HttpPost("items")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AltaItemDto>> CreateItem(AltaItemDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = dto.ToModel();
        _db.AltaItems.Add(item);
        await _db.SaveChangesAsync();
        dto.Id = item.Id;
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, dto);
    }

    [HttpPut("items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateItem(int id, AltaItemDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var item = await _db.AltaItems.FindAsync(id);
        if (item == null) return NotFound();
        item.Url = dto.Url;
        item.Number = dto.Number;
        item.Code = dto.Code;
        item.Name = dto.Name;
        item.Comment = dto.Comment;
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteItem(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = await _db.AltaItems.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // CRUD for AltaExceptions
    [HttpGet("exceptions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AltaExceptionDto>))]
    public async Task<ActionResult<IEnumerable<AltaExceptionDto>>> GetExceptions()
    {
        var items = await _db.AltaExceptions.AsNoTracking().ToListAsync();
        return items.Select(i => new AltaExceptionDto(i)).ToList();
    }

    [HttpGet("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AltaExceptionDto))]
    public async Task<ActionResult<AltaExceptionDto>> GetException(int id)
    {
        var item = await _db.AltaExceptions.FindAsync(id);
        return item == null ? _404Object(id) : new AltaExceptionDto(item);
    }

    [HttpPost("exceptions")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AltaExceptionDto>> CreateException(AltaExceptionDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = dto.ToModel();
        _db.AltaExceptions.Add(item);
        await _db.SaveChangesAsync();
        dto.Id = item.Id;
        return CreatedAtAction(nameof(GetException), new { id = item.Id }, dto);
    }

    [HttpPut("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateException(int id, AltaExceptionDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var item = await _db.AltaExceptions.FindAsync(id);
        if (item == null) return NotFound();
        item.Url = dto.Url;
        item.Number = dto.Number;
        item.Code = dto.Code;
        item.Name = dto.Name;
        item.Comment = dto.Comment;
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteException(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = await _db.AltaExceptions.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaExceptions.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
