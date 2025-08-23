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
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class FeacnInsertItemsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<FeacnInsertItemsController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;

    private ObjectResult _409InsertItem(string code)
    {
        return StatusCode(StatusCodes.Status409Conflict,
            new ErrMessage { Msg = $"Запись с таким кодом уже существует [код = {code}]" });
    }

    private async Task<bool> _logistOrAdmin()
    {
        if (await _userService.CheckLogist(_curUserId)) return true;
        return await _userService.CheckAdmin(_curUserId);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnInsertItemDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<FeacnInsertItemDto>>> GetItems()
    {
        if (!await _logistOrAdmin()) return _403();
        var items = await _db.FeacnInsertItems.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
        return items.Select(i => new FeacnInsertItemDto(i)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnInsertItemDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnInsertItemDto>> GetItem(int id)
    {
        if (!await _logistOrAdmin()) return _403();
        var item = await _db.FeacnInsertItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return item == null ? _404Object(id) : new FeacnInsertItemDto(item);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(FeacnInsertItemDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnInsertItemDto>> CreateItem(FeacnInsertItemDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            dto.Code.Length != FeacnCode.FeacnCodeLength ||
            !dto.Code.All(char.IsDigit))
        {
            return _400MustBe10Digits(dto.Code);
        }
        if (await _db.FeacnInsertItems.AnyAsync(i => i.Code == dto.Code))
        {
            return _409InsertItem(dto.Code);
        }

        var item = dto.ToModel();
        _db.FeacnInsertItems.Add(item);
        try
        {
            await _db.SaveChangesAsync();
            dto.Id = item.Id;
            return CreatedAtAction(nameof(GetItem), new { id = item.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_insert_items_code") == true)
        {
            _logger.LogDebug("CreateItem returning '409 Conflict' due to database constraint");
            return _409InsertItem(dto.Code);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateItem(int id, FeacnInsertItemDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            dto.Code.Length != FeacnCode.FeacnCodeLength ||
            !dto.Code.All(char.IsDigit))
        {
            return _400MustBe10Digits(dto.Code);
        }

        var item = await _db.FeacnInsertItems.FindAsync(id);
        if (item == null) return _404Object(id);

        if (!item.Code.Equals(dto.Code, StringComparison.OrdinalIgnoreCase) &&
            await _db.FeacnInsertItems.AnyAsync(i => i.Code == dto.Code))
        {
            return _409InsertItem(dto.Code);
        }

        item.Code = dto.Code;
        item.InsertBefore = dto.InsertBefore;
        item.InsertAfter = dto.InsertAfter;

        try
        {
            _db.Entry(item).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_insert_items_code") == true)
        {
            _logger.LogDebug("UpdateItem returning '409 Conflict' due to database constraint");
            return _409InsertItem(dto.Code);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteItem(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var item = await _db.FeacnInsertItems.FindAsync(id);
        if (item == null) return _404Object(id);
        _db.FeacnInsertItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

