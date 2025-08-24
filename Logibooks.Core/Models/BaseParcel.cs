// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using ClosedXML.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("base_parcels")]
[Index(nameof(TnVed), Name = "IX_base_parcels_tn_ved")]
[Index(nameof(RegisterId), nameof(CheckStatusId), nameof(Id), Name = "IX_base_parcels_registerid_checkstatusid_id")]
public abstract class BaseParcel
{
    [Column("id")]
    public int Id { get; set; }

    [Column("register_id")]
    public int RegisterId { get; set; }

    [JsonIgnore]
    public Register Register { get; set; } = null!;

    [Column("status_id")]
    public int StatusId { get; set; }
    public ParcelStatus Status { get; set; } = null!;

    [Column("check_status_id")]
    public int CheckStatusId { get; set; }
    public ParcelCheckStatus CheckStatus { get; set; } = null!;

    [Column("product_name")]
    public string? ProductName { get; set; }

    [Column("tn_ved")]
    public string? TnVed { get; set; }

    [Column("country_code")]
    public short CountryCode { get; set; } = 643; 

    [JsonIgnore]
    public Country? Country { get; set; }

    [Column("partner_color")]
    [JsonIgnore]
    public uint PartnerColor { get; set; }

    [NotMapped]
    [JsonIgnore]
    public XLColor PartnerColorXL
    {
        get => XLColor.FromArgb((int)PartnerColor);
        set => PartnerColor = (uint)value.Color.ToArgb();
    }

    public ICollection<BaseOrderStopWord> BaseOrderStopWords { get; set; } = [];
    public ICollection<BaseParcelFeacnPrefix> BaseOrderFeacnPrefixes { get; set; } = [];
    public ICollection<BaseOrderKeyWord> BaseOrderKeyWords { get; set; } = [];

    // IndPost generation API
    public abstract string GetParcelNumber();
    public abstract string GetCurrency();
    public abstract string GetDescription(string? insertBefore, string? insertAfter);
    public abstract string GetQuantity();
    public abstract string GetCost();
    public abstract string GetWeight();
    public abstract string GetUrl();
    public abstract string GetCity();
    public abstract string GetStreet();
    public abstract string GetSurName();
    public abstract string GetName();
    public abstract string GetMiddleName();
    public abstract string GetFullName();
    public abstract string GetSeries();
    public abstract string GetNumber();

    public string GetTnVed() => TnVed ?? string.Empty;
    public static string FormatCost(decimal? cost) => cost?.ToString("F2", new CultureInfo("en-US")) ?? "0.00";
    public static string FormatWeight(decimal? weight) => weight?.ToString("F3", new CultureInfo("en-US")) ?? "0.000";
}
