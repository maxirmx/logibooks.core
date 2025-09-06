// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnCodeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CodeEx { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int? ParentId { get; set; }

    public FeacnCodeDto()
    {
    }

    public FeacnCodeDto(FeacnCode code)
    {
        Id = code.Id;
        Code = code.Code;
        CodeEx = code.CodeEx;
        Name = code.Name;
        NormalizedName = code.NormalizedName;
        ParentId = code.ParentId;
    }
}

