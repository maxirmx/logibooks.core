// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("word_match_types")]
public class WordMatchType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [JsonIgnore]
    public ICollection<StopWord> StopWords { get; set; } = [];

    [JsonIgnore]
    public ICollection<KeyWord> KeyWords { get; set; } = [];
}
