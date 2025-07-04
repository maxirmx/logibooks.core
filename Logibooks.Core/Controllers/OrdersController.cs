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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<OrdersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private const int MaxPageSize = 1000;

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderViewItem>> GetOrder(int id)
    {
        _logger.LogDebug("GetOrder for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            _logger.LogDebug("GetOrder returning '404 Not Found'");
            return _404Order(id);
        }

        _logger.LogDebug("GetOrder returning order");
        return new OrderViewItem(order);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateOrder(int id, OrderUpdateItem update)
    {
        _logger.LogDebug("UpdateOrder for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UpdateOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found'");
            return _404Order(id);
        }

        // Copy allowed properties from update to entity
        foreach (var prop in typeof(OrderUpdateItem).GetProperties())
        {
            if (prop.Name == nameof(Order.RegisterId) || prop.Name == nameof(Order.Id))
                continue;

            var val = prop.GetValue(update);
            if (val != null)
            {
                typeof(Order).GetProperty(prop.Name)?.SetValue(order, val);
            }
        }

        _db.Entry(order).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("UpdateOrder returning '204 No content'");
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<OrderViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PagedResult<OrderViewItem>>> GetOrders(
        int registerId,
        int? statusId = null,
        string? tnVed = null,
        int page = 1,
        int pageSize = 100,
        string? sortBy = null,
        string sortOrder = "asc")
    {
        _logger.LogDebug("GetOrders for register={reg} status={st} tnVed={tnVed} page={page} size={size} sortBy={sortBy} sortOrder={sortOrder}",
            registerId, statusId, tnVed, page, pageSize, sortBy, sortOrder);

        if (page <= 0 || pageSize <= 0 || pageSize > MaxPageSize)
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid pagination");
            return _400();
        }

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        var allowedSortBy = new[] { "id", "status", "tnved" };
        if (!allowedSortBy.Contains(sortBy.ToLower()))
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortBy");
            return _400();
        }

        if (sortOrder != "asc" && sortOrder != "desc")
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortOrder");
            return _400();
        }

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrders returning '403 Forbidden'");
            return _403();
        }

        IQueryable<Order> query = _db.Orders.AsNoTracking().Where(o => o.RegisterId == registerId);

        if (statusId != null)
        {
            query = query.Where(o => o.StatusId == statusId);
        }

        if (!string.IsNullOrWhiteSpace(tnVed))
        {
            query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
        }

        query = (sortBy.ToLower(), sortOrder) switch
        {
            ("status", "asc") => query.OrderBy(o => o.StatusId),
            ("status", "desc") => query.OrderByDescending(o => o.StatusId),
            ("tnved", "asc") => query.OrderBy(o => o.TnVed),
            ("tnved", "desc") => query.OrderByDescending(o => o.TnVed),
            ("id", "desc") => query.OrderByDescending(o => o.Id),
            _ => query.OrderBy(o => o.Id)
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewItems = items.Select(o => new OrderViewItem(o)).ToList();

        var result = new PagedResult<OrderViewItem>
        {
            Items = viewItems,
            Pagination = new PaginationInfo
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1
            },
            Sorting = new SortingInfo { SortBy = sortBy, SortOrder = sortOrder }
        };

        _logger.LogDebug("GetOrders returning {count} items", items.Count);
        return Ok(result);
    }
}

