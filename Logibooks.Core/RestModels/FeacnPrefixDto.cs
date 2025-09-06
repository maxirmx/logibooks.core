// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnPrefixDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Comment { get; set; }
    public int? FeacnOrderId { get; set; }
    public List<FeacnPrefixExceptionDto> Exceptions { get; set; } = [];
    public FeacnPrefixDto(FeacnPrefix p)
    {
        Id = p.Id;
        Code = (p.IntervalCode is not null) ? $"{p.Code}-{p.IntervalCode}" : p.Code;
        Description = p.Description;
        Comment = p.Comment;
        FeacnOrderId = p.FeacnOrderId;
        Exceptions = [.. p.FeacnPrefixExceptions
            .OrderBy(e => e.Id)
            .Select(e => new FeacnPrefixExceptionDto(e))];
    }
}
