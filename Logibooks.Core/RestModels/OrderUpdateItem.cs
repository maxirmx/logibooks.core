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

using System.Text.Json;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class OrderUpdateItem
{
    public int? StatusId { get; set; }
    public int? RowNumber { get; set; }
    public string? OrderNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? Sticker { get; set; }
    public string? Shk { get; set; }
    public string? StickerCode { get; set; }
    public string? ExtId { get; set; }
    public string? TnVed { get; set; }
    public string? SiteArticle { get; set; }
    public string? HeelHeight { get; set; }
    public string? Size { get; set; }
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public string? Gender { get; set; }
    public string? Brand { get; set; }
    public string? FabricType { get; set; }
    public string? Composition { get; set; }
    public string? Lining { get; set; }
    public string? Insole { get; set; }
    public string? Sole { get; set; }
    public string? Country { get; set; }
    public string? FactoryAddress { get; set; }
    public string? Unit { get; set; }
    public string? WeightKg { get; set; }
    public string? Quantity { get; set; }
    public string? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public string? Barcode { get; set; }
    public string? Declaration { get; set; }
    public string? ProductLink { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientInn { get; set; }
    public string? PassportNumber { get; set; }
    public string? Pinfl { get; set; }
    public string? RecipientAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? BoxNumber { get; set; }
    public string? Supplier { get; set; }
    public string? SupplierInn { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public string? PersonalData { get; set; }
    public string? CustomsClearance { get; set; }
    public string? DutyPayment { get; set; }
    public string? OtherReason { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
