// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks.Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Data;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class AuthController(
    AppDbContext db, 
    IJwtUtils jwtUtils,
    ILogger<AuthController> logger) : LogibooksControllerPreBase(db, logger)
{
    private readonly IJwtUtils _jwtUtils = jwtUtils;

    // POST: api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserViewItemWithJWT))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public async Task<ActionResult<UserViewItem>> Login(Credentials crd)
    {
        _logger.LogDebug("Login attempt for {email}", crd.Email);

        User? user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.Email.ToLower() == crd.Email.ToLower())
            .SingleOrDefaultAsync();

        if (user == null) return _401();

        if (!BCrypt.Net.BCrypt.Verify(crd.Password, user.Password)) return _401();
        if (!user.HasAnyRole()) return _403();

        UserViewItemWithJWT userViewItem = new(user)
        {
            Token = _jwtUtils.GenerateJwtToken(user),
        };

        _logger.LogDebug("Login returning\n{res}", userViewItem.ToString());
        return userViewItem;
    }

    // GET: api/auth/check
    // Checks authorization status
    [HttpGet("check")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    public IActionResult Check()
    {
        _logger.LogDebug("Check authorization status");
        return NoContent();
    }

}

