// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("companies")]
public class Company
{
    [Column("id")]
    public int Id { get; set; }

    [Column("inn")]
    public string Inn { get; set; } = string.Empty;

    [Column("kpp")]
    public string Kpp { get; set; } = string.Empty;

    [Column("ogrn")]
    public string Ogrn { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("short_name")]
    public string ShortName { get; set; } = string.Empty;

    [Column("country_iso_numeric")]
    public short CountryIsoNumeric { get; set; }

    [JsonIgnore]
    public Country Country { get; set; } = null!;

    [Column("postal_code")]
    public string PostalCode { get; set; } = string.Empty;

    [Column("city")]
    public string City { get; set; } = string.Empty;

    [Column("street")]
    public string Street { get; set; } = string.Empty;

    [JsonIgnore]
    public ICollection<Register> Registers { get; set; } = [];

    [JsonIgnore]
    public ICollection<Register> TheOtherRegisters { get; set; } = [];
}
