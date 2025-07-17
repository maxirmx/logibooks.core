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
using Logibooks.Core.Authorization;

using Logibooks.Core.Services;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CountriesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUpdateCountriesService service,
    ILogger<CountriesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUpdateCountriesService _service = service;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CountryDto>))]
    public async Task<ActionResult<IEnumerable<CountryDto>>> GetCodes()
    {
        var codes = await _db.Countries.AsNoTracking().ToListAsync();
        return codes.Select(c => new CountryDto(c)).ToList();
    }

    [HttpGet("compact")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CountryCompactDto>))]
    public async Task<ActionResult<IEnumerable<CountryCompactDto>>> GetCodesCompact()
    {
        var codes = await _db.Countries.AsNoTracking()
            .OrderBy(c =>
                c.IsoAlpha2 == "RU" ? 0 :
                c.IsoAlpha2 == "UZ" ? 1 :
                c.IsoAlpha2 == "GE" ? 2 :
                c.IsoAlpha2 == "AZ" ? 3 :
                c.IsoAlpha2 == "TR" ? 4 :
                int.MaxValue)
            .ThenBy(c => c.IsoNumeric)
            .Select(c => new CountryCompactDto(c))
            .ToListAsync();
        return codes;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CountryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<CountryDto>> GetCode(short id)
    {
        var cc = await _db.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.IsoNumeric == id);
        return cc == null ? _404Object(id) : new CountryDto(cc);
    }

    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Update()
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();

        try
        {
            await _service.RunAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Update returning '500 Internal Server Error'");
            return _500UploadCountryCodes();
        }

        return NoContent();
    }
}
