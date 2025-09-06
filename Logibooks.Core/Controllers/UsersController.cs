// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Text.Json;

using Logibooks.Core.Authorization;
using Logibooks.Core.RestModels;
using Logibooks.Core.Settings;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class UsersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<UsersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
    // GET: api/users
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<UserViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<UserViewItem>>> GetUsers()
    {
        _logger.LogDebug("GetUsers");
        var ch = await _userService.CheckAdmin(_curUserId);
        if (!ch)
        {
            if (!ch)
            _logger.LogDebug("GetUsers returning '403 Forbidden'");
            return _403();
        }

        var res = await _userService.UserViewItems();
        _logger.LogDebug("GetUsers returning:\n{items}\n", JsonSerializer.Serialize(res, JOptions.DefaultOptions));

        return res;
    }

    // GET: api/users/5
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<UserViewItem>> GetUser(int id)
    {
        _logger.LogDebug("GetUser for id={id}", id);
        var ch = await _userService.CheckAdminOrSameUser(id, _curUserId);
        if (!ch)
        {
            _logger.LogDebug("GetUser returning '403 Forbidden'");
            return _403();
        }

        var user = await _userService.UserViewItem(id);
        if (user == null)
        {
            _logger.LogDebug("GetUser returning '404 Not Found'");
            return _404User(id);
        }

        _logger.LogDebug("GetUser returning:\n{res}", user.ToString());
        return user;
    }

    // POST: api/users
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> PostUser(UserCreateItem user)
    {
        _logger.LogDebug("PostUser (create) for {user}", user.ToString());
        var ch = await _userService.CheckAdmin(_curUserId);
        if (!ch)
        {
            _logger.LogDebug("PostUser returning '403 Forbidden'");
            return _403();
        }

        if (_userService.Exists(user.Email))
        {
            _logger.LogDebug("PostUser returning '409 Conflict'");
            return _409Email(user.Email);
        }

        try
        {
            string hashToStoreInDb = BCrypt.Net.BCrypt.HashPassword(user.Password);

            User ur = new()
            {
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Patronymic = user.Patronymic ?? "",
                Email = user.Email,
                Password = hashToStoreInDb,
            };

            _db.Users.Add(ur);
            await _db.SaveChangesAsync(); // This assigns ur.Id

            if (user.Roles != null && user.Roles.Count > 0)
            {
                var rolesInDb = _db.Roles.Where(r => user.Roles.Contains(r.Name)).ToList();
                foreach (var role in rolesInDb)
                {
                    _db.UserRoles.Add(new UserRole { UserId = ur.Id, RoleId = role.Id });
                }
                await _db.SaveChangesAsync(); // Save the user roles
            }

            var reference = new Reference { Id = ur.Id };
            _logger.LogDebug("PostUser returning: {res}", reference.ToString());
            return CreatedAtAction(nameof(PostUser), new { id = ur.Id }, reference);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_users_email") == true)
        {
            // Handle database constraint violation (race condition case)
            _logger.LogDebug("PostUser returning '409 Conflict' due to database constraint");
            return _409Email(user.Email);
        }
    }


    // PUT: api/users/5
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutUser(int id, UserUpdateItem update)
    {
        _logger.LogDebug("PutUser (update) for id={id} with {update}", id, update.ToString());
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogDebug("PutUser returning '404 Not Found'");
            return _404User(id);
        }
        bool adminRequired = update.IsAdministrator() && !user.IsAdministrator();

        ActionResult<bool> ch;
        ch = adminRequired ? await _userService.CheckAdmin(_curUserId) :
                             await _userService.CheckAdminOrSameUser(id, _curUserId);
        if (ch == null || !ch.Value)
        {
            _logger.LogDebug("PutUser returning '403 Forbidden'");
            return _403();
        }

        if (update.Email != null && user.Email != update.Email)
        {
            if (_userService.Exists(update.Email)) return _409Email(update.Email);
            user.Email = update.Email;
        }

        if (update.FirstName != null) user.FirstName = update.FirstName;
        if (update.LastName != null) user.LastName = update.LastName;
        if (update.Patronymic != null) user.Patronymic = update.Patronymic;

        // Copy user roles from update to database
        if (update.Roles != null && update.Roles.Count > 0)
        {
            // Remove existing roles
            var existingUserRoles = _db.UserRoles.Where(ur => ur.UserId == user.Id);
            _db.UserRoles.RemoveRange(existingUserRoles);

            // Add new roles
            var rolesInDb = _db.Roles.Where(r => update.Roles.Contains(r.Name)).ToList();
            foreach (var role in rolesInDb)
            {
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        if (update.Password != null) user.Password = BCrypt.Net.BCrypt.HashPassword(update.Password);

        _db.Entry(user).State = EntityState.Modified;
        try
        {
            await _db.SaveChangesAsync();

            _logger.LogDebug("PutUser returning '204 No content'");
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_users_email") == true)
        {
            // Handle database constraint violation (race condition case)
            _logger.LogDebug("PutUser returning '409 Conflict' due to database constraint");
            return _409Email(update.Email ?? string.Empty);
        }
    }

    // DELETE: api/users/5
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteUser(int id)
    {
        _logger.LogDebug("DeleteUser for id={id}", id);
        var ch = await _userService.CheckAdmin(_curUserId);
        if (!ch)
        {
            _logger.LogDebug("DeleteUser returning '403 Forbidden'");
            return _403();
        }
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogDebug("DeleteUser returning '404 Not Found'");
            return _404User(id);
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        _logger.LogDebug("DeleteUser returning '204 No content'");
        return NoContent();
    }

}
