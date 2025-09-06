// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class WordMatchTypesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<WordMatchTypesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<WordMatchTypeDto>))]
    public async Task<ActionResult<IEnumerable<WordMatchTypeDto>>> GetWordMatchTypes()
    {
        var matchTypes = await _db.WordMatchTypes.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        _logger.LogDebug("GetWordMatchTypes returning {count} items", matchTypes.Count);
        return matchTypes.Select(mt => new WordMatchTypeDto(mt)).ToList();
    }
}