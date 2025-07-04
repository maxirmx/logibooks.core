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

using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Extensions;

/// <summary>
/// Extension methods for Order class
/// </summary>
public static class OrderExtensions
{
    /// <summary>
    /// Updates the current order with values from the provided OrderUpdateItem.
    /// Only non-null values from the update item will be applied.
    /// </summary>
    /// <param name="order">The order to update</param>
    /// <param name="updateItem">The update item containing new values</param>
    public static void UpdateFrom(this Order order, OrderUpdateItem updateItem)
    {
        if (updateItem.StatusId.HasValue) order.StatusId = updateItem.StatusId.Value;
        if (updateItem.RowNumber.HasValue) order.RowNumber = updateItem.RowNumber.Value;
        if (updateItem.OrderNumber != null) order.OrderNumber = updateItem.OrderNumber;
        if (updateItem.InvoiceDate != null) order.InvoiceDate = updateItem.InvoiceDate;
        if (updateItem.Sticker != null) order.Sticker = updateItem.Sticker;
        if (updateItem.Shk != null) order.Shk = updateItem.Shk;
        if (updateItem.StickerCode != null) order.StickerCode = updateItem.StickerCode;
        if (updateItem.ExtId != null) order.ExtId = updateItem.ExtId;
        if (updateItem.TnVed != null) order.TnVed = updateItem.TnVed;
        if (updateItem.SiteArticle != null) order.SiteArticle = updateItem.SiteArticle;
        if (updateItem.HeelHeight != null) order.HeelHeight = updateItem.HeelHeight;
        if (updateItem.Size != null) order.Size = updateItem.Size;
        if (updateItem.ProductName != null) order.ProductName = updateItem.ProductName;
        if (updateItem.Description != null) order.Description = updateItem.Description;
        if (updateItem.Gender != null) order.Gender = updateItem.Gender;
        if (updateItem.Brand != null) order.Brand = updateItem.Brand;
        if (updateItem.FabricType != null) order.FabricType = updateItem.FabricType;
        if (updateItem.Composition != null) order.Composition = updateItem.Composition;
        if (updateItem.Lining != null) order.Lining = updateItem.Lining;
        if (updateItem.Insole != null) order.Insole = updateItem.Insole;
        if (updateItem.Sole != null) order.Sole = updateItem.Sole;
        if (updateItem.Country != null) order.Country = updateItem.Country;
        if (updateItem.FactoryAddress != null) order.FactoryAddress = updateItem.FactoryAddress;
        if (updateItem.Unit != null) order.Unit = updateItem.Unit;
        if (updateItem.WeightKg != null) order.WeightKg = updateItem.WeightKg;
        if (updateItem.Quantity != null) order.Quantity = updateItem.Quantity;
        if (updateItem.UnitPrice != null) order.UnitPrice = updateItem.UnitPrice;
        if (updateItem.Currency != null) order.Currency = updateItem.Currency;
        if (updateItem.Barcode != null) order.Barcode = updateItem.Barcode;
        if (updateItem.Declaration != null) order.Declaration = updateItem.Declaration;
        if (updateItem.ProductLink != null) order.ProductLink = updateItem.ProductLink;
        if (updateItem.RecipientName != null) order.RecipientName = updateItem.RecipientName;
        if (updateItem.RecipientInn != null) order.RecipientInn = updateItem.RecipientInn;
        if (updateItem.PassportNumber != null) order.PassportNumber = updateItem.PassportNumber;
        if (updateItem.Pinfl != null) order.Pinfl = updateItem.Pinfl;
        if (updateItem.RecipientAddress != null) order.RecipientAddress = updateItem.RecipientAddress;
        if (updateItem.ContactPhone != null) order.ContactPhone = updateItem.ContactPhone;
        if (updateItem.BoxNumber != null) order.BoxNumber = updateItem.BoxNumber;
        if (updateItem.Supplier != null) order.Supplier = updateItem.Supplier;
        if (updateItem.SupplierInn != null) order.SupplierInn = updateItem.SupplierInn;
        if (updateItem.Category != null) order.Category = updateItem.Category;
        if (updateItem.Subcategory != null) order.Subcategory = updateItem.Subcategory;
        if (updateItem.PersonalData != null) order.PersonalData = updateItem.PersonalData;
        if (updateItem.CustomsClearance != null) order.CustomsClearance = updateItem.CustomsClearance;
        if (updateItem.DutyPayment != null) order.DutyPayment = updateItem.DutyPayment;
        if (updateItem.OtherReason != null) order.OtherReason = updateItem.OtherReason;
    }
}

