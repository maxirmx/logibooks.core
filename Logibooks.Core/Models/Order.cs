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

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using Logibooks.Core.RestModels;

namespace Logibooks.Core.Models;

[Table("orders")]
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

    [Column("row_number")]
    public int RowNumber { get; set; }

    [Column("order_number")]
    public string? OrderNumber { get; set; }

    [Column("invoice_date")]
    public string? InvoiceDate { get; set; }

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

    [Column("weight_kg")]
    public string? WeightKg { get; set; }

    [Column("quantity")]
    public string? Quantity { get; set; }

    [Column("unit_price")]
    public string? UnitPrice { get; set; }

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

    public void UpdateFrom(OrderUpdateItem updateItem)
    {
        if (updateItem.StatusId.HasValue) StatusId = updateItem.StatusId.Value;
        if (updateItem.RowNumber.HasValue) RowNumber = updateItem.RowNumber.Value;
        if (updateItem.OrderNumber != null) OrderNumber = updateItem.OrderNumber;
        if (updateItem.InvoiceDate != null) InvoiceDate = updateItem.InvoiceDate;
        if (updateItem.Sticker != null) Sticker = updateItem.Sticker;
        if (updateItem.Shk != null) Shk = updateItem.Shk;
        if (updateItem.StickerCode != null) StickerCode = updateItem.StickerCode;
        if (updateItem.ExtId != null) ExtId = updateItem.ExtId;
        if (updateItem.TnVed != null) TnVed = updateItem.TnVed;
        if (updateItem.SiteArticle != null) SiteArticle = updateItem.SiteArticle;
        if (updateItem.HeelHeight != null) HeelHeight = updateItem.HeelHeight;
        if (updateItem.Size != null) Size = updateItem.Size;
        if (updateItem.ProductName != null) ProductName = updateItem.ProductName;
        if (updateItem.Description != null) Description = updateItem.Description;
        if (updateItem.Gender != null) Gender = updateItem.Gender;
        if (updateItem.Brand != null) Brand = updateItem.Brand;
        if (updateItem.FabricType != null) FabricType = updateItem.FabricType;
        if (updateItem.Composition != null) Composition = updateItem.Composition;
        if (updateItem.Lining != null) Lining = updateItem.Lining;
        if (updateItem.Insole != null) Insole = updateItem.Insole;
        if (updateItem.Sole != null) Sole = updateItem.Sole;
        if (updateItem.Country != null) Country = updateItem.Country;
        if (updateItem.FactoryAddress != null) FactoryAddress = updateItem.FactoryAddress;
        if (updateItem.Unit != null) Unit = updateItem.Unit;
        if (updateItem.WeightKg != null) WeightKg = updateItem.WeightKg;
        if (updateItem.Quantity != null) Quantity = updateItem.Quantity;
        if (updateItem.UnitPrice != null) UnitPrice = updateItem.UnitPrice;
        if (updateItem.Currency != null) Currency = updateItem.Currency;
        if (updateItem.Barcode != null) Barcode = updateItem.Barcode;
        if (updateItem.Declaration != null) Declaration = updateItem.Declaration;
        if (updateItem.ProductLink != null) ProductLink = updateItem.ProductLink;
        if (updateItem.RecipientName != null) RecipientName = updateItem.RecipientName;
        if (updateItem.RecipientInn != null) RecipientInn = updateItem.RecipientInn;
        if (updateItem.PassportNumber != null) PassportNumber = updateItem.PassportNumber;
        if (updateItem.Pinfl != null) Pinfl = updateItem.Pinfl;
        if (updateItem.RecipientAddress != null) RecipientAddress = updateItem.RecipientAddress;
        if (updateItem.ContactPhone != null) ContactPhone = updateItem.ContactPhone;
        if (updateItem.BoxNumber != null) BoxNumber = updateItem.BoxNumber;
        if (updateItem.Supplier != null) Supplier = updateItem.Supplier;
        if (updateItem.SupplierInn != null) SupplierInn = updateItem.SupplierInn;
        if (updateItem.Category != null) Category = updateItem.Category;
        if (updateItem.Subcategory != null) Subcategory = updateItem.Subcategory;
        if (updateItem.PersonalData != null) PersonalData = updateItem.PersonalData;
        if (updateItem.CustomsClearance != null) CustomsClearance = updateItem.CustomsClearance;
        if (updateItem.DutyPayment != null) DutyPayment = updateItem.DutyPayment;
        if (updateItem.OtherReason != null) OtherReason = updateItem.OtherReason;
    }
}
