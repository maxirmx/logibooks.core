using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class ParcelStatusesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<ParcelStatusesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<OrderStatusDto>))]
    public async Task<ActionResult<IEnumerable<OrderStatusDto>>> GetStatuses()
    {
        var statuses = await _db.Statuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        return statuses.Select(s => new OrderStatusDto(s)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderStatusDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderStatusDto>> GetStatus(int id)
    {
        var status = await _db.Statuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (status == null) return _404Object(id);
        return new OrderStatusDto(status);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> CreateStatus(OrderStatusDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
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
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var status = await _db.Statuses.FindAsync(id);
        if (status == null) return _404Object(id);
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
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
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
