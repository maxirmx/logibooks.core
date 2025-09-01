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
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class FeacnPrefixesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<FeacnPrefixesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnPrefixDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<FeacnPrefixDto>>> GetPrefixes()
    {
        if (!await _userService.CheckLogist(_curUserId)) return _403();
        var prefixes = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .Where(p => p.FeacnOrderId == null)
            .OrderBy(p => p.Id)
            .Select(p => new FeacnPrefixDto(p))
            .ToListAsync();
        return prefixes;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnPrefixDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnPrefixDto>> GetPrefix(int id)
    {
        if (!await _userService.CheckLogist(_curUserId)) return _403();
        var prefix = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);
        return new FeacnPrefixDto(prefix);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> CreatePrefix(FeacnPrefixCreateDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var prefix = dto.ToModel();
        _db.FeacnPrefixes.Add(prefix);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPrefix), new { id = prefix.Id }, new Reference { Id = prefix.Id });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdatePrefix(int id, FeacnPrefixCreateDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var prefix = await _db.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);

        prefix.Code = dto.Code;
        prefix.IntervalCode = dto.IntervalCode;
        prefix.Description = dto.Description;
        prefix.Comment = dto.Comment;

        _db.FeacnPrefixExceptions.RemoveRange(prefix.FeacnPrefixExceptions);
        prefix.FeacnPrefixExceptions = dto.Exceptions
            .Select(e => new FeacnPrefixException { Code = e, FeacnPrefixId = id })
            .ToList();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeletePrefix(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var prefix = await _db.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);

        _db.FeacnPrefixExceptions.RemoveRange(prefix.FeacnPrefixExceptions);
        _db.FeacnPrefixes.Remove(prefix);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

