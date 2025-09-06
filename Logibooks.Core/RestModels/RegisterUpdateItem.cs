// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class RegisterUpdateItem
{
    public string? DealNumber { get; set; }
    public int? TheOtherCompanyId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public short? TheOtherCountryCode { get; set; }
    public int? TransportationTypeId { get; set; }
    public int? CustomsProcedureId { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
