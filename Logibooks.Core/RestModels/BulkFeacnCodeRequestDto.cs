// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

public class BulkFeacnCodeRequestDto
{
    public string[] Codes { get; set; } = Array.Empty<string>();

    public BulkFeacnCodeRequestDto()
    {
    }

    public BulkFeacnCodeRequestDto(string[] codes)
    {
        Codes = codes;
    }
}