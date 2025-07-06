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

using Logibooks.Core.Data;
using Logibooks.Core.Services;
using Logibooks.Core.Authorization;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class AltaController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<AltaController> logger,
    HttpClient? httpClient = null) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly HttpClient? _httpClient = httpClient;
    // POST api/alta/parse
    [HttpPost("parse")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<int>> Parse()
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();

        var urls = new List<string>
        {
            "https://www.alta.ru/tamdoc/11pr0971/",
            "https://www.alta.ru/tamdoc/22ps0311/",
            "https://www.alta.ru/tamdoc/10sr0317/",
            "https://www.alta.ru/tamdoc/22ps0312/",
            "https://www.alta.ru/tamdoc/10sr0318/"
        };

        var (items, exceptions) = await AltaParser.ParseAsync(urls, _httpClient);

        // Filter out items with codes that already exist in the database
        var existingItemCodes = await _db.AltaItems.Select(x => x.Code).ToListAsync();
        var newItems = items.Where(x => !existingItemCodes.Contains(x.Code)).ToList();

        var existingExceptionCodes = await _db.AltaExceptions.Select(x => x.Code).ToListAsync();
        var newExceptions = exceptions.Where(x => !existingExceptionCodes.Contains(x.Code)).ToList();

        if (newItems.Count != 0) _db.AltaItems.AddRange(newItems);
        if (newExceptions.Count != 0) _db.AltaExceptions.AddRange(newExceptions);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // CRUD for AltaItems
    [HttpGet("items")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AltaItemDto>))]
    public async Task<ActionResult<IEnumerable<AltaItemDto>>> GetItems()
    {
        var items = await _db.AltaItems.AsNoTracking().ToListAsync();
        return items.Select(i => new AltaItemDto(i)).ToList();
    }

    [HttpGet("items/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AltaItemDto))]
    public async Task<ActionResult<AltaItemDto>> GetItem(int id)
    {
        var item = await _db.AltaItems.FindAsync(id);
        return item == null ? _404Object(id) : new AltaItemDto(item);
    }

    [HttpPost("items")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AltaItemDto>> CreateItem(AltaItemDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();

        if (await _db.AltaItemCodeExists(dto.Code))
        {
            return _409AltaItemCode(dto.Code);
        }

        var item = dto.ToModel();
        _db.AltaItems.Add(item);
        await _db.SaveChangesAsync();
        dto.Id = item.Id;
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, dto);
    }

    [HttpPut("items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateItem(int id, AltaItemDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        var item = await _db.AltaItems.FindAsync(id);
        if (item == null) return NotFound();

        // Check for code conflict only if the code is being changed
        if (item.Code != dto.Code && await _db.AltaItemCodeExists(dto.Code))
        {
            return _409AltaItemCode(dto.Code);
        }

        item.Url = dto.Url;
        item.Number = dto.Number;
        item.Code = dto.Code;
        item.Name = dto.Name;
        item.Comment = dto.Comment;
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("items/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteItem(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = await _db.AltaItems.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // CRUD for AltaExceptions
    [HttpGet("exceptions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<AltaExceptionDto>))]
    public async Task<ActionResult<IEnumerable<AltaExceptionDto>>> GetExceptions()
    {
        var items = await _db.AltaExceptions.AsNoTracking().ToListAsync();
        return items.Select(i => new AltaExceptionDto(i)).ToList();
    }

    [HttpGet("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AltaExceptionDto))]
    public async Task<ActionResult<AltaExceptionDto>> GetException(int id)
    {
        var item = await _db.AltaExceptions.FindAsync(id);
        return item == null ? _404Object(id) : new AltaExceptionDto(item);
    }

    [HttpPost("exceptions")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<AltaExceptionDto>> CreateException(AltaExceptionDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();

        if (await _db.AltaExceptionCodeExists(dto.Code))
        {
            return _409AltaExceptionCode(dto.Code);
        }

        var item = dto.ToModel();
        _db.AltaExceptions.Add(item);
        await _db.SaveChangesAsync();
        dto.Id = item.Id;
        return CreatedAtAction(nameof(GetException), new { id = item.Id }, dto);
    }

    [HttpPut("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateException(int id, AltaExceptionDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        var item = await _db.AltaExceptions.FindAsync(id);
        if (item == null) return NotFound();

        // Check for code conflict only if the code is being changed
        if (item.Code != dto.Code && await _db.AltaExceptionCodeExists(dto.Code))
        {
            return _409AltaExceptionCode(dto.Code);
        }

        item.Url = dto.Url;
        item.Number = dto.Number;
        item.Code = dto.Code;
        item.Name = dto.Name;
        item.Comment = dto.Comment;
        _db.Entry(item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("exceptions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteException(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var item = await _db.AltaExceptions.FindAsync(id);
        if (item == null) return NotFound();
        _db.AltaExceptions.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}