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
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE),
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Text.Json;
using System.Linq;
using Logibooks.Core.Models;
using Logibooks.Core.Settings;
using DocumentFormat.OpenXml.InkML;

namespace Logibooks.Core.RestModels;

public class OrderViewItem
{
    public int Id { get; set; }
    public int RegisterId { get; set; }
    public int StatusId { get; set; }
    public int CheckStatusId { get; set; }
    public string? ProductName { get; set; }
    public string? TnVed { get; set; }
    public string? OrderNumber { get; set; }
    public string? Shk { get; set; }
    public string? Description { get; set; }
    public short CountryCode { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Currency { get; set; }
    public string? ProductLink { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientInn { get; set; }
    public string? PassportNumber { get; set; }
    public int? PlacesCount { get; set; }
    public string? Article { get; set; }
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? Patronymic { get; set; }
    public string? PostingNumber { get; set; }
    public string? OzonId { get; set; }
    public List<int> StopWordIds { get; set; } = [];
    public List<int> FeacnOrderIds { get; set; } = [];
    public Timestamp? LastView { get; set; } = null;

    public OrderViewItem(BaseOrder order)
    {
        Id = order.Id;
        RegisterId = order.RegisterId;
        StatusId = order.StatusId;
        CheckStatusId = order.CheckStatusId;
        ProductName = order.ProductName;
        TnVed = order.TnVed;

        CountryCode = order.CountryCode;

        if (order is WbrOrder wbr)
        {
            OrderNumber = wbr.OrderNumber;
            Shk = wbr.Shk;
            Description = wbr.Description;
            WeightKg = wbr.WeightKg;
            Quantity = wbr.Quantity;
            UnitPrice = wbr.UnitPrice;
            Currency = wbr.Currency;
            ProductLink = wbr.ProductLink;
            RecipientName = wbr.RecipientName;
            RecipientInn = wbr.RecipientInn;
            PassportNumber = wbr.PassportNumber;
        }
        else if (order is OzonOrder ozon)
        {
            PostingNumber = ozon.PostingNumber;
            OzonId = ozon.OzonId;
            WeightKg = ozon.WeightKg;
            Quantity = ozon.Quantity;
            UnitPrice = ozon.UnitPrice;
            Currency = ozon.Currency;
            ProductLink = ozon.ProductLink;
            PassportNumber = ozon.PassportNumber;
            PlacesCount = ozon.PlacesCount;
            Article = ozon.Article;
            LastName = ozon.LastName;
            FirstName = ozon.FirstName;
            Patronymic = ozon.Patronymic;
        }

        StopWordIds = order.BaseOrderStopWords?
            .Select(bosw => bosw.StopWordId)
            .ToList() ?? new List<int>();
        FeacnOrderIds = order.BaseOrderFeacnPrefixes?
            .Where(bofp => bofp.FeacnPrefix?.FeacnOrderId != null)
            .Select(bofp => bofp.FeacnPrefix?.FeacnOrderId ?? 0)
            .Distinct()
            .ToList() ?? new List<int>();
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
