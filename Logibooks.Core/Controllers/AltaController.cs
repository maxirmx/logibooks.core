using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AltaController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<AltaController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    // POST api/alta/parse
    [HttpPost("parse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> Parse([FromBody] List<string> urls)
    {
        var (items, exceptions) = await AltaParser.ParseAsync(urls);
        if (items.Count != 0) _db.AltaItems.AddRange(items);
        if (exceptions.Count != 0) _db.AltaExceptions.AddRange(exceptions);
        await _db.SaveChangesAsync();
        return items.Count;
    }

    // CRUD for AltaItems
    [HttpGet("items")]
    public async Task<IEnumerable<AltaItem>> GetItems() =>
        await _db.AltaItems.AsNoTracking().ToListAsync();

    [HttpGet("items/{id}")]
    public async Task<ActionResult<AltaItem>> GetItem(int id)
    {
        var item = await _db.AltaItems.FindAsync(id);
        return item == null ? _404Object(id) : item; 
    }

    [HttpPost("items")]
    public async Task<ActionResult<AltaItem>> CreateItem(AltaItem item)
    {
        _db.AltaItems.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(int id, AltaItem item)
    {
        if (id != item.Id) return BadRequest();
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var item = await _db.AltaItems.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // CRUD for AltaExceptions
    [HttpGet("exceptions")]
    public async Task<IEnumerable<AltaException>> GetExceptions() =>
        await _db.AltaExceptions.AsNoTracking().ToListAsync();

    [HttpGet("exceptions/{id}")]
    public async Task<ActionResult<AltaException>> GetException(int id)
    {
        var item = await _db.AltaExceptions.FindAsync(id);
        return item == null ? _404Object(id) : item;
    }

    [HttpPost("exceptions")]
    public async Task<ActionResult<AltaException>> CreateException(AltaException item)
    {
        _db.AltaExceptions.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetException), new { id = item.Id }, item);
    }

    [HttpPut("exceptions/{id}")]
    public async Task<IActionResult> UpdateException(int id, AltaException item)
    {
        if (id != item.Id) return BadRequest();
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("exceptions/{id}")]
    public async Task<IActionResult> DeleteException(int id)
    {
        var item = await _db.AltaExceptions.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaExceptions.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
