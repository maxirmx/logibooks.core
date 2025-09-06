// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class FeacnInsertItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? InsBefore { get; set; }
    public string? InsAfter { get; set; }

    public FeacnInsertItemDto() {}

    public FeacnInsertItemDto(FeacnInsertItem item)
    {
        Id = item.Id;
        Code = item.Code;
        InsBefore = item.InsertBefore;
        InsAfter = item.InsertAfter;
    }

    public FeacnInsertItem ToModel()
    {
        return new FeacnInsertItem
        {
            Id = Id,
            Code = Code,
            InsertBefore = InsBefore,
            InsertAfter = InsAfter
        };
    }
}

