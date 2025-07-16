using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FeacnController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<FeacnController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnDataDto))]
    public async Task<ActionResult<FeacnDataDto>> GetAll()
    {
        var orders = await _db.FEACNOrders.AsNoTracking().OrderBy(o => o.Id).ToListAsync();
        var prefixes = await _db.FEACNPrefixes.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        var exceptions = await _db.FEACNPrefixExceptions.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
        var dto = new FeacnDataDto
        {
            Orders = orders.Select(o => new FeacnOrderDto(o)).ToList(),
            Prefixes = prefixes.Select(p => new FeacnPrefixDto(p)).ToList(),
            Exceptions = exceptions.Select(e => new FeacnPrefixExceptionDto(e)).ToList()
        };
        return dto;
    }

    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Update()
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        return NoContent();
    }
}
