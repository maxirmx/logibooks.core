// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Services;

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class UserInformationService(AppDbContext db) : IUserInformationService
{
    private readonly AppDbContext _db = db;

    public async Task<bool> CheckAdmin(int cuid)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(x => x.Id == cuid)
            .FirstOrDefaultAsync();
        return user != null && user.IsAdministrator();
    }

    public async Task<bool> CheckLogist(int cuid)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(x => x.Id == cuid)
            .FirstOrDefaultAsync();
        return user != null && user.IsLogist();
    }

    public async Task<bool> CheckAdminOrSameUser(int id, int cuid)
    {
        if (cuid == 0) return false;
        if (cuid == id) return true;
        return await CheckAdmin(cuid);
    }

    public bool CheckSameUser(int id, int cuid)
    {
        if (cuid == 0) return false;
        if (cuid == id) return true;
        return false;
    }

    public bool Exists(int id)
    {
        return _db.Users.AsNoTracking().Any(e => e.Id == id);
    }

    public bool Exists(string email)
    {
        return _db.Users.AsNoTracking().Any(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<UserViewItem?> UserViewItem(int id)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(x => x.Id == id)
            .Select(x => new UserViewItem(x))
            .FirstOrDefaultAsync();
        return user;
    }

    public async Task<List<UserViewItem>> UserViewItems()
    {
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Select(x => new UserViewItem(x))
            .ToListAsync();
    }
}
