// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_orders")]
public class FeacnOrder
{
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("url")]
    private string? _url;
    public string? Url
    {
        get => string.IsNullOrEmpty(_url) ? null : $"https://www.alta.ru/tamdoc/{_url}/";
        set => _url = value;
    } 

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;
    public ICollection<FeacnPrefix> FeacnPrefixes { get; set; } = [];
}
