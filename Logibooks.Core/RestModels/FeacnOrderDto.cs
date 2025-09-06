// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnOrderDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Comment { get; set; }
    public bool Enabled { get; set; }
    public FeacnOrderDto(FeacnOrder o)
    {
        Id = o.Id;
        Title = o.Title;
        Url = o.Url;
        Comment = o.Comment;
        Enabled = o.Enabled;
    }
}
