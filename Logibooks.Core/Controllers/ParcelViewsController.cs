// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Add(Reference dto)
    {
        // Validate that the BaseOrderId exists in the database
        var orderExists = await _db.Parcels.AnyAsync(o => o.Id == dto.Id);
        if (!orderExists)
        {
            return _404Parcel(dto.Id);
        }

        var pv = new ParcelView
        {
            UserId = _curUserId,
            BaseParcelId = dto.Id,
            DTime = DateTime.UtcNow
        };
        _db.ParcelViews.Add(pv);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ParcelViewItem))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ParcelViewItem>> Back()
    {
        var items = await _db.ParcelViews
            .Where(v => v.UserId == _curUserId)
            .OrderByDescending(v => v.DTime)
            .Take(2)
            .Include(v => v.BaseParcel)
            .ToListAsync();

        if (items.Count == 0)
            return NoContent();

        _db.ParcelViews.Remove(items[0]);
        if (items.Count == 1)
        {
            await _db.SaveChangesAsync();
            return NoContent();
        }

        var appliedView = items[1];
        var order = appliedView.BaseParcel;
        if (order == null)
        {
            await _db.SaveChangesAsync();
            return NoContent();
        }

        var result = new ParcelViewItem(order)
        {
            DTime = appliedView.DTime
        };

        _db.ParcelViews.Remove(appliedView);
        await _db.SaveChangesAsync();
        return Ok(result);
    }
}
