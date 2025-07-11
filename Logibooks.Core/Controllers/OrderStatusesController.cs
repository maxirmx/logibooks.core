using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrderStatusesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<OrderStatusesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<OrderStatusDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<OrderStatusDto>>> GetStatuses()
    {
        if (!await _db.CheckLogist(_curUserId)) return _403();
        var statuses = await _db.Statuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        return statuses.Select(s => new OrderStatusDto(s)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderStatusDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderStatusDto>> GetStatus(int id)
    {
        if (!await _db.CheckLogist(_curUserId)) return _403();
        var status = await _db.Statuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (status == null) return _404Object(id);
        return new OrderStatusDto(status);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderStatusDto>> CreateStatus(OrderStatusDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var status = dto.ToModel();
        _db.Statuses.Add(status);
        await _db.SaveChangesAsync();
        dto.Id = status.Id;
        return CreatedAtAction(nameof(GetStatus), new { id = status.Id }, dto);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatusDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var status = await _db.Statuses.FindAsync(id);
        if (status == null) return _404Object(id);
        status.Name = dto.Name;
        status.Title = dto.Title;
        _db.Entry(status).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteStatus(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var status = await _db.Statuses.FindAsync(id);
        if (status == null) return _404Object(id);

        bool hasOrders = await _db.Orders.AnyAsync(r => r.StatusId == id);
        if (hasOrders)
        {
            return _409OrderStatus();
        }

        _db.Statuses.Remove(status);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return _409OrderStatus();
        }
        return NoContent();
    }
}
