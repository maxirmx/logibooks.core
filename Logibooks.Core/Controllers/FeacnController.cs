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
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnDataDto>> GetAll()
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAll returning '500 Internal Server Error'");
            return _500UploadFeacn();
        }
    }

    private async Task<List<TDto>> FetchAndConvertAsync<TEntity, TDto>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? filter,
        Func<TEntity, TDto> convertToDto)
        where TEntity : class
    {
        if (filter != null)
        {
            query = query.Where(filter);
        }
        var entities = await query.AsNoTracking().OrderBy(e => EF.Property<object>(e, "Id")).ToListAsync();
        return entities.Select(convertToDto).ToList();
    }

    [HttpGet("orders")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnOrderDto>))]
    public async Task<ActionResult<IEnumerable<FeacnOrderDto>>> GetAllOrders()
    {
        var orders = await FetchAndConvertAsync(_db.FEACNOrders, null, o => new FeacnOrderDto(o));
        return orders;
    }

    [HttpGet("orders/{orderId}/prefixes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnPrefixDto>))]
    public async Task<ActionResult<IEnumerable<FeacnPrefixDto>>> GetPrefixes(int orderId)
    {
        var prefixes = await FetchAndConvertAsync(
            _db.FEACNPrefixes,
            p => p.FeacnOrderId == orderId,
            p => new FeacnPrefixDto(p));
        return prefixes;
    }

    [HttpGet("prefixes/{prefixId}/exceptions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnPrefixExceptionDto>))]
    public async Task<ActionResult<IEnumerable<FeacnPrefixExceptionDto>>> GetPrefixException(int prefixId)
    {
        var exceptions = await FetchAndConvertAsync(
            _db.FEACNPrefixExceptions,
            e => e.FeacnPrefixId == prefixId,
            e => new FeacnPrefixExceptionDto(e));
        return exceptions;
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
