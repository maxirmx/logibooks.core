// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class StatusController(
    AppDbContext db,
    ILogger<StatusController> logger) : LogibooksControllerPreBase(db, logger)
{
    // GET: api/auth/status
    // Checks service status
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json", Type = typeof(Status))]
    public async Task<ActionResult<Status>> Status()
    {
        _logger.LogDebug("Check service status");

        // Get the last migration timestamp from the database
        string dbVersion = "Unknown";
        try
        {
            // Query the __EFMigrationsHistory table for the last applied migration
            var lastMigration = await _db.Database.GetAppliedMigrationsAsync();
            dbVersion = lastMigration.LastOrDefault() ?? "00000000000000";
            // Truncate dbVersion up to the first '_' if present
            if (dbVersion.Contains('_'))
            {
                dbVersion = dbVersion[..dbVersion.IndexOf('_')];
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving migration history");
            dbVersion = "00000000000000";
        }

        Status status = new()
        {
            Msg = "Hello, world! Logibooks Core status is fantastic!",
            AppVersion = VersionInfo.AppVersion,
            DbVersion = dbVersion,
        };

        _logger.LogDebug("Check service status returning:\n{status}", status);
        return Ok(status);
    }
}
