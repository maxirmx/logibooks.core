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
public class TransportationTypesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<TransportationTypesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<TransportationTypeDto>))]
    public async Task<ActionResult<IEnumerable<TransportationTypeDto>>> GetTypes()
    {
        var list = await _db.TransportationTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        return list.Select(t => new TransportationTypeDto(t)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransportationTypeDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<TransportationTypeDto>> GetType(int id)
    {
        var type = await _db.TransportationTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return type == null ? _404Object(id) : new TransportationTypeDto(type);
    }
}
