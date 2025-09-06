// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class ParcelUpdateItem
{
    public int? StatusId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Shk { get; set; }
    public string? TnVed { get; set; }
    public string? ProductName { get; set; }
    public short? CountryCode { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public string? ProductLink { get; set; }
    public string? RecipientName { get; set; }
    public string? PassportNumber { get; set; }

    // Ozon specific fields
    public string? PostingNumber { get; set; }
    public int? PlacesCount { get; set; }
    public string? Article { get; set; }
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? Patronymic { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
