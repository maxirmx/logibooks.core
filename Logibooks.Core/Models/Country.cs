// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("countries")]
public class Country
{
    [Column("iso_numeric")]
    public short IsoNumeric { get; set; }

    [Column("iso_alpha2")]
    public required string IsoAlpha2 { get; set; }

    [Column("name_en_short")]
    public string NameEnShort { get; set; } = string.Empty;

    [Column("name_en_formal")]
    public string NameEnFormal { get; set; } = string.Empty;

    [Column("name_en_official")]
    public string NameEnOfficial { get; set; } = string.Empty;

    [Column("name_en_cldr")]
    public string NameEnCldr { get; set; } = string.Empty;

    [Column("name_ru_short")]
    public string NameRuShort { get; set; } = string.Empty;

    [Column("name_ru_formal")]
    public string NameRuFormal { get; set; } = string.Empty;

    [Column("name_ru_official")]
    public string NameRuOfficial { get; set; } = string.Empty;

    [Column("loaded_at")]
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<Company> Companies { get; set; } = [];

}
