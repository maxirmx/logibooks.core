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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("orders")]
[Index(nameof(TnVed), Name = "IX_orders_tn_ved")]
[Index(nameof(Shk), Name = "IX_orders_shk")]
public class Order
{
    [Column("id")]
    public int Id { get; set; }

    [Column("register_id")]
    public int RegisterId { get; set; }

    [JsonIgnore]
    public Register Register { get; set; } = null!;

    [Column("status_id")]
    public int StatusId { get; set; }
    public OrderStatus Status { get; set; } = null!;

    [Column("check_status_id")]
    public int CheckStatusId { get; set; }
    public OrderCheckStatus CheckStatus { get; set; } = null!;

    [Column("row_number")]
    public int RowNumber { get; set; }

    [Column("order_number")]
    public string? OrderNumber { get; set; }

    [Column("invoice_date", TypeName = "date")]
    public DateOnly? InvoiceDate { get; set; }

    [Column("sticker")]
    public string? Sticker { get; set; }

    [Column("shk")]
    public string? Shk { get; set; }

    [Column("sticker_code")]
    public string? StickerCode { get; set; }

    [Column("ext_id")]
    public string? ExtId { get; set; }

    [Column("tn_ved")]
    public string? TnVed { get; set; }

    [Column("site_article")]
    public string? SiteArticle { get; set; }

    [Column("heel_height")]
    public string? HeelHeight { get; set; }

    [Column("size")]
    public string? Size { get; set; }

    [Column("product_name")]
    public string? ProductName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    [Column("brand")]
    public string? Brand { get; set; }

    [Column("fabric_type")]
    public string? FabricType { get; set; }

    [Column("composition")]
    public string? Composition { get; set; }

    [Column("lining")]
    public string? Lining { get; set; }

    [Column("insole")]
    public string? Insole { get; set; }

    [Column("sole")]
    public string? Sole { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("factory_address")]
    public string? FactoryAddress { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("weight_kg", TypeName = "numeric(10,3)")]
    public decimal? WeightKg { get; set; }

    [Column("quantity", TypeName = "numeric(10,3)")]
    public decimal? Quantity { get; set; }

    [Column("unit_price", TypeName = "numeric(10,3)")]
    public decimal? UnitPrice { get; set; }

    [Column("currency")]
    public string? Currency { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("declaration")]
    public string? Declaration { get; set; }

    [Column("product_link")]
    public string? ProductLink { get; set; }

    [Column("recipient_name")]
    public string? RecipientName { get; set; }

    [Column("recipient_inn")]
    public string? RecipientInn { get; set; }

    [Column("passport_number")]
    public string? PassportNumber { get; set; }

    [Column("pinfl")]
    public string? Pinfl { get; set; }

    [Column("recipient_address")]
    public string? RecipientAddress { get; set; }

    [Column("contact_phone")]
    public string? ContactPhone { get; set; }

    [Column("box_number")]
    public string? BoxNumber { get; set; }

    [Column("supplier")]
    public string? Supplier { get; set; }

    [Column("supplier_inn")]
    public string? SupplierInn { get; set; }

    [Column("category")]
    public string? Category { get; set; }

    [Column("subcategory")]
    public string? Subcategory { get; set; }

    [Column("personal_data")]
    public string? PersonalData { get; set; }

    [Column("customs_clearance")]
    public string? CustomsClearance { get; set; }

    [Column("duty_payment")]
    public string? DutyPayment { get; set; }

    [Column("other_reason")]
    public string? OtherReason { get; set; }
}

