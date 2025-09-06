// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json;

using Logibooks.Core.Models;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class UserViewItem(User user)
{
    public int Id { get; set; } = user.Id;
    public string FirstName { get; set; } = user.FirstName;
    public string LastName { get; set; } = user.LastName;
    public string Patronymic { get; set; } = user.Patronymic;
    public string Email { get; set; } = user.Email;
    public List<string> Roles { get; set; } =
        [.. user.UserRoles.Select(ur => ur.Role!.Name)];
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }

}
