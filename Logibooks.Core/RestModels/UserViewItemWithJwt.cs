// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json;

using Logibooks.Core.Models;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class UserViewItemWithJWT(User user) : UserViewItem(user)
{
    public string Token { get; set; } = "";
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
