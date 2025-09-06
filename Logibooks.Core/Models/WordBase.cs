// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[NotMapped]
public abstract class WordBase
{
    [Column("id")]
    public int Id { get; set; }

    [Column("word")]
    public required string Word { get; set; } = string.Empty;

    [Column("match_type_id")]
    public int MatchTypeId { get; set; } = 1;
    public WordMatchType MatchType { get; set; } = null!;
}

