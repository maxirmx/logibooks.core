// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;
public class Status
{
    public required string Msg { get; set; }
    public required string AppVersion { get; set; }
    public required string DbVersion { get; set; }

}
