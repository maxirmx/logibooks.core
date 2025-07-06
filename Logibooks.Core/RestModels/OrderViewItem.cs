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
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE),
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Text.Json;
using Logibooks.Core.Models;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class OrderViewItem(Order order)
{
    public int Id { get; set; } = order.Id;
    public int RegisterId { get; set; } = order.RegisterId;
    public int StatusId { get; set; } = order.StatusId;
    public int RowNumber { get; set; } = order.RowNumber;
    public string? OrderNumber { get; set; } = order.OrderNumber;
    public DateOnly? InvoiceDate { get; set; } = order.InvoiceDate;
    public string? Sticker { get; set; } = order.Sticker;
    public string? Shk { get; set; } = order.Shk;
    public string? StickerCode { get; set; } = order.StickerCode;
    public string? ExtId { get; set; } = order.ExtId;
    public string? TnVed { get; set; } = order.TnVed;
    public string? SiteArticle { get; set; } = order.SiteArticle;
    public string? HeelHeight { get; set; } = order.HeelHeight;
    public string? Size { get; set; } = order.Size;
    public string? ProductName { get; set; } = order.ProductName;
    public string? Description { get; set; } = order.Description;
    public string? Gender { get; set; } = order.Gender;
    public string? Brand { get; set; } = order.Brand;
    public string? FabricType { get; set; } = order.FabricType;
    public string? Composition { get; set; } = order.Composition;
    public string? Lining { get; set; } = order.Lining;
    public string? Insole { get; set; } = order.Insole;
    public string? Sole { get; set; } = order.Sole;
    public string? Country { get; set; } = order.Country;
    public string? FactoryAddress { get; set; } = order.FactoryAddress;
    public string? Unit { get; set; } = order.Unit;
    public decimal? WeightKg { get; set; } = order.WeightKg;
    public decimal? Quantity { get; set; } = order.Quantity;
    public decimal? UnitPrice { get; set; } = order.UnitPrice;
    public string? Currency { get; set; } = order.Currency;
    public string? Barcode { get; set; } = order.Barcode;
    public string? Declaration { get; set; } = order.Declaration;
    public string? ProductLink { get; set; } = order.ProductLink;
    public string? RecipientName { get; set; } = order.RecipientName;
    public string? RecipientInn { get; set; } = order.RecipientInn;
    public string? PassportNumber { get; set; } = order.PassportNumber;
    public string? Pinfl { get; set; } = order.Pinfl;
    public string? RecipientAddress { get; set; } = order.RecipientAddress;
    public string? ContactPhone { get; set; } = order.ContactPhone;
    public string? BoxNumber { get; set; } = order.BoxNumber;
    public string? Supplier { get; set; } = order.Supplier;
    public string? SupplierInn { get; set; } = order.SupplierInn;
    public string? Category { get; set; } = order.Category;
    public string? Subcategory { get; set; } = order.Subcategory;
    public string? PersonalData { get; set; } = order.PersonalData;
    public string? CustomsClearance { get; set; } = order.CustomsClearance;
    public string? DutyPayment { get; set; } = order.DutyPayment;
    public string? OtherReason { get; set; } = order.OtherReason;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
