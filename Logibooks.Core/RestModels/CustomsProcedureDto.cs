// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class CustomsProcedureDto
{
    public int Id { get; set; }
    public short Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public CustomsProcedureDto() {}
    public CustomsProcedureDto(CustomsProcedure cp)
    {
        Id = cp.Id;
        Code = cp.Code;
        Name = cp.Name;
    }

    public CustomsProcedure ToModel()
    {
        return new CustomsProcedure
        {
            Id = Id,
            Code = Code,
            Name = Name
        };
    }
}
