// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnPrefixCreateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? IntervalCode { get; set; }
    public string? Description { get; set; }
    public string? Comment { get; set; }
    public List<string> Exceptions { get; set; } = [];

    public FeacnPrefix ToModel()
    {
        return new FeacnPrefix
        {
            Id = Id,
            Code = Code,
            IntervalCode = IntervalCode,
            Description = Description,
            Comment = Comment,
            FeacnOrderId = null,
            FeacnPrefixExceptions = [.. Exceptions.Select(e => new FeacnPrefixException { Code = e })]
        };
    }
}
