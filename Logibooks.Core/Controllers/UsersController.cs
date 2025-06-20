// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core applcation
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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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

using System.Text.Json;

using Logibooks.Core.Authorization;
using Logibooks.Core.RestModels;
using Logibooks.Core.Settings;
using Logibooks.Core.Data;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]

public class UsersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<UsersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    // GET: api/users
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<UserViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<UserViewItem>>> GetUsers()
    {
        _logger.LogDebug("GetUsers");
        var ch = await _db.CheckAdmin(_curUserId);
        if (ch == null || !ch.Value)
        {
            _logger.LogDebug("GetUsers returning '403 Forbidden'");
            return _403();
        }

        var res = await _db.UserViewItems();
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
        var ch = await _db.CheckAdminOrSameUser(id, _curUserId);
        if (ch == null || !ch.Value)
        {
            _logger.LogDebug("GetUser returning '403 Forbidden'");
            return _403();
        }

        var user = await _db.UserViewItem(id);
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> PostUser(User user)
    {
        _logger.LogDebug("PostUser (create) for {user}", user.ToString());
        var ch = await _db.CheckAdmin(_curUserId);
        if (ch == null || !ch.Value)
        {
            _logger.LogDebug("PostUser returning '403 Forbidden'");
            return _403();
        }

        if (_db.Exists(user.Email))
        {
            _logger.LogDebug("PostUser returning '409 Conflict'");
            return _409Email(user.Email);
        }

        string hashToStoreInDb = BCrypt.Net.BCrypt.HashPassword(user.Password);
        user.Password = hashToStoreInDb;

        _db.Users.Add(user);

        await _db.SaveChangesAsync();
        var reference = new Reference(user.Id) { Id = user.Id };
        _logger.LogDebug("PostUser returning:\n{res}", reference.ToString());
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, reference);
    }

    // PUT: api/users/5
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutUser(int id, UserUpdateItem update)
    {
        _logger.LogDebug("PutUser (update) for id={id} with {update}", id, update.ToString());
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogDebug("PutUser returning '404 Not Found'");
            return _404User(id);
        }
        bool adminRequired =  (user.IsAdministrator() != update.IsAdministrator());

        ActionResult<bool> ch;
        ch = adminRequired ? await _db.CheckAdmin(_curUserId) :
                             await _db.CheckAdminOrSameUser(id, _curUserId);
        if (ch == null || !ch.Value)
        {
            _logger.LogDebug("PutUser returning '403 Forbidden'");
            return _403();
        }

        if (update.Email != null && user.Email != update.Email)
        {
            if (_db.Exists(update.Email)) return _409Email(update.Email);
            user.Email = update.Email;
        }

        if (update.FirstName != null) user.FirstName = update.FirstName;
        if (update.LastName != null) user.LastName = update.LastName;
        if (update.Patronimic != null) user.Patronimic = update.Patronimic;

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
        await _db.SaveChangesAsync();
        
        _logger.LogDebug("PutUser returning '204 No content'");
        return NoContent();
    }

    // DELETE: api/users/5
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteUser(int id)
    {
        _logger.LogDebug("DeleteUser for id={id}", id);
        var ch = await _db.CheckAdmin(_curUserId);
        if (ch == null || !ch.Value)
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
