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
