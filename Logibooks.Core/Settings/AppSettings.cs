// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Settings;

public class AppSettings
{
    public string? Secret { get; set; } = null;
    public int JwtTokenExpirationDays { get; set; } = 7;
}
