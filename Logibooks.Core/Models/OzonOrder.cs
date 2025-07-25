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

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("ozon_orders")]
public class OzonOrder : BaseOrder
{
    [Column("posting_number")]
    public string? PostingNumber { get; set; }

    [Column("ozon_id")]
    public string? OzonId { get; set; }

    [Column("box_number")]
    public string? BoxNumber { get; set; }

    [Column("shipment_weight_kg", TypeName = "numeric(10,3)")]
    public decimal? ShipmentWeightKg { get; set; }

    [Column("places_count")]
    public int? PlacesCount { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("article")]
    public string? Article { get; set; }

    [Column("manufacturer")]
    public string? Manufacturer { get; set; }


    [Column("description_en")]
    public string? DescriptionEn { get; set; }

    [Column("weight_kg", TypeName = "numeric(10,2)")]
    public decimal? WeightKg { get; set; }

    [Column("currency")]
    public string? Currency { get; set; }

    [Column("unit_price", TypeName = "numeric(10,2)")]
    public decimal? UnitPrice { get; set; }

    [Column("quantity", TypeName = "numeric(10)")]
    public decimal? Quantity { get; set; }

    [Column("product_link")]
    public string? ProductLink { get; set; }

    [Column("last_name")]
    public string? LastName { get; set; }

    [Column("first_name")]
    public string? FirstName { get; set; }

    [Column("patronymic")]
    public string? Patronymic { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("birth_date", TypeName = "date")]
    public DateOnly? BirthDate { get; set; }

    [Column("passport_series")]
    public string? PassportSeries { get; set; }

    [Column("passport_number")]
    public string? PassportNumber { get; set; }

    [Column("passport_issue_date", TypeName = "date")]
    public DateOnly? PassportIssueDate { get; set; }

    [Column("passport_issued_by")]
    public string? PassportIssuedBy { get; set; }

    [Column("inn")]
    public string? Inn { get; set; }

    [Column("postal_code")]
    public string? PostalCode { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("cmn")]
    public string? Cmn { get; set; }

    [Column("cmn_id")]
    public string? CmnId { get; set; }

    [Column("imei")]
    public string? Imei { get; set; }

    [Column("imei_2")]
    public string? Imei2 { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }
}
