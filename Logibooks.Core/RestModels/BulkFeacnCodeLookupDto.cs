// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

public class BulkFeacnCodeLookupDto
{
    public Dictionary<string, FeacnCodeDto?> Results { get; set; } = new Dictionary<string, FeacnCodeDto?>();

    public BulkFeacnCodeLookupDto()
    {
    }

    public BulkFeacnCodeLookupDto(Dictionary<string, FeacnCodeDto?> results)
    {
        Results = results;
    }
}