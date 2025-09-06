// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application


namespace Logibooks.Core.RestModels;

public class UserCreateItem
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Patronymic { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public List<string> Roles { get; set; } = [];
    public bool HasRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        return Roles != null && Roles.Any(ur => string.Equals(ur, roleName, StringComparison.OrdinalIgnoreCase));
    }

}
