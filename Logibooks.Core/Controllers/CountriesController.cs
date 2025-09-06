// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class CountriesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    IUpdateCountriesService service,
    ILogger<CountriesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
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
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        await _service.RunAsync();
        return NoContent();
    }
}
