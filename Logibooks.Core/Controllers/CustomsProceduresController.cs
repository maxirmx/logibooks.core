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
public class CustomsProceduresController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<CustomsProceduresController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CustomsProcedureDto>))]
    public async Task<ActionResult<IEnumerable<CustomsProcedureDto>>> GetProcedures()
    {
        var list = await _db.CustomsProcedures.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        return list.Select(p => new CustomsProcedureDto(p)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomsProcedureDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<CustomsProcedureDto>> GetProcedure(int id)
    {
        var proc = await _db.CustomsProcedures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (proc == null) return _404Object(id);
        return new CustomsProcedureDto(proc);
    }
}
