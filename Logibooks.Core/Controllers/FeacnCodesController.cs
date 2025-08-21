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
using System.Linq;

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
public class FeacnCodesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<FeacnCodesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnCodeDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnCodeDto>> Get(int id)
    {
        var code = await _db.FeacnCodes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return code == null ? _404Object(id) : new FeacnCodeDto(code);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnCodeDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnCodeDto>> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            code.Length != FeacnCode.FeacnCodeLength ||
            !code.All(char.IsDigit))
        {
            return _400MustBe10Digits(code);
        }

        var fc = await _db.FeacnCodes.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code);
        return fc == null ? _404FeacnCode(code) : new FeacnCodeDto(fc);
    }

    [HttpGet("lookup/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnCodeDto>))]
    public async Task<ActionResult<IEnumerable<FeacnCodeDto>>> Lookup(string key)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upperKey = key.ToUpper();
        var codes = await _db.FeacnCodes.AsNoTracking()
            .Where(c => (c.FromDate == null || c.FromDate <= today) &&
                        c.NormalizedName.Contains(upperKey))
            .OrderBy(c => c.Id)
            .Select(c => new FeacnCodeDto(c))
            .ToListAsync();
        return codes;
    }

    [HttpGet("children")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnCodeDto>))]
    public async Task<ActionResult<IEnumerable<FeacnCodeDto>>> Children(int? id)
    {
        IQueryable<FeacnCode> query = _db.FeacnCodes.AsNoTracking();
        query = id.HasValue ? query.Where(c => c.ParentId == id.Value) : query.Where(c => c.ParentId == null);
        var codes = await query
            .OrderBy(c => c.Id)
            .Select(c => new FeacnCodeDto(c))
            .ToListAsync();
        return codes;
    }
}

