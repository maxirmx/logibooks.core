// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using System.Linq.Expressions;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class FeacnCodesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    IUpdateFeacnCodesService service,
    ILogger<FeacnCodesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
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
