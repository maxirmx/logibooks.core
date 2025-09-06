// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using System.Linq.Expressions;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class FeacnOrdersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    IUpdateFeacnCodesService service,
    ILogger<FeacnOrdersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
    private readonly IUpdateFeacnCodesService _service = service;

    private static async Task<List<TDto>> FetchAndConvertAsync<TEntity, TDto>(
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
        var orders = await FetchAndConvertAsync(_db.FeacnOrders, null, o => new FeacnOrderDto(o));
        return orders;
    }

    [HttpGet("orders/{orderId}/prefixes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnPrefixDto>))]
    public async Task<ActionResult<IEnumerable<FeacnPrefixDto>>> GetPrefixes(int orderId)
    {
        var prefixes = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .Where(p => p.FeacnOrderId == orderId)
            .OrderBy(p => p.Id)
            .Select(p => new FeacnPrefixDto(p))
            .ToListAsync();
        return prefixes;
    }

    [HttpPost("orders/{orderId}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> EnableOrder(int orderId)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var order = await _db.FeacnOrders.FindAsync(orderId);
        if (order == null) return _404FeacnOrder(orderId);
        order.Enabled = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("orders/{orderId}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DisableOrder(int orderId)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var order = await _db.FeacnOrders.FindAsync(orderId);
        if (order == null) return _404FeacnOrder(orderId);
        order.Enabled = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Update()
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        await _service.RunAsync();
        return NoContent();
    }
}
