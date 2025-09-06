// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json.Serialization;

namespace Logibooks.Core.RestModels;

public class RegisterViewItem
{
    public int Id { get; set; }
    public string DealNumber { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public int TheOtherCompanyId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public short? TheOtherCountryCode { get; set; }
    public int TransportationTypeId { get; set; }
    public int CustomsProcedureId { get; set; }
    public int ParcelsTotal { get; set; }
    public int PlacesTotal { get; set; }
    public Dictionary<int, int> ParcelsByCheckStatus { get; set; } = new();

    [JsonIgnore]
    public string CompanyShortName { get; set; } = string.Empty;
    [JsonIgnore]
    public string NameRuOfficial { get; set; } = string.Empty;
    [JsonIgnore]
    public string TransportationTypeName { get; set; } = string.Empty;
    [JsonIgnore]
    public string CustomsProcedureName { get; set; } = string.Empty;
}
