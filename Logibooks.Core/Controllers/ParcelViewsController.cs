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
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class ParcelViewsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<ParcelViewsController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Add(Reference dto)
    {
        var pv = new ParcelView
        {
            UserId = _curUserId,
            BaseOrderId = dto.Id,
            DTime = DateTime.UtcNow
        };
        _db.ParcelViews.Add(pv);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<Reference>> Back()
    {
        var pv = await _db.ParcelViews
                          .Where(v => v.UserId == _curUserId)
                          .OrderByDescending(v => v.DTime)
                          .FirstOrDefaultAsync();
        if (pv == null) return NoContent();
        var res = new Reference { Id = pv.BaseOrderId };
        _db.ParcelViews.Remove(pv);
        await _db.SaveChangesAsync();
        return res;
    }
}
