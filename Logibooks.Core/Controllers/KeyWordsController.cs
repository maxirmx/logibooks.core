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
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class KeyWordsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<KeyWordsController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<KeyWordDto>))]
    public async Task<ActionResult<IEnumerable<KeyWordDto>>> GetKeyWords()
    {
        var words = await _db.KeyWords.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        return words.Select(w => new KeyWordDto(w)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(KeyWordDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<KeyWordDto>> GetKeyWord(int id)
    {
        var word = await _db.KeyWords.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        return word == null ? _404Object(id) : new KeyWordDto(word);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(KeyWordDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<KeyWordDto>> PostKeyWord(KeyWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();

        if (await _db.KeyWords.AnyAsync(sw => sw.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409KeyWord(dto.Word);
        }

        var kw = dto.ToModel();
        _db.KeyWords.Add(kw);
        try
        {
            await _db.SaveChangesAsync();
            dto.Id = kw.Id;
            return CreatedAtAction(nameof(GetKeyWord), new { id = kw.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_key_words_word") == true)
        {
            _logger.LogDebug("PostKeyWord returning '409 Conflict' due to database constraint");
            return _409KeyWord(dto.Word);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutKeyWord(int id, KeyWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        var kw = await _db.KeyWords.FindAsync(id);
        if (kw == null) return _404Object(id);

        if (!kw.Word.Equals(dto.Word, StringComparison.OrdinalIgnoreCase) &&
            await _db.KeyWords.AnyAsync(w => w.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409KeyWord(dto.Word);
        }

        kw.Word = dto.Word;
        kw.MatchTypeId = dto.MatchTypeId;
        kw.FeacnCode = dto.FeacnCode;

        try
        {
            _db.Entry(kw).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_key_words_word") == true)
        {
            _logger.LogDebug("PutKeyWord returning '409 Conflict' due to database constraint");
            return _409KeyWord(dto.Word);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteKeyWord(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var kw = await _db.KeyWords.FindAsync(id);
        if (kw == null) return _404Object(id);

        _db.KeyWords.Remove(kw);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
