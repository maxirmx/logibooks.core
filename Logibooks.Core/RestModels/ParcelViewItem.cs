// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Text.Json;
using Logibooks.Core.Models;
using Logibooks.Core.Settings;

namespace Logibooks.Core.RestModels;

public class ParcelViewItem
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
    public List<int> KeyWordIds { get; set; } = [];
    public List<int> FeacnOrderIds { get; set; } = [];
    public List<int> FeacnPrefixIds { get; set; } = [];
    public DateTime? DTime { get; set; }

    public ParcelViewItem(BaseParcel parcel)
    {
        Id = parcel.Id;
        RegisterId = parcel.RegisterId;
        StatusId = parcel.StatusId;
        CheckStatusId = parcel.CheckStatusId;
        ProductName = parcel.ProductName;
        TnVed = parcel.TnVed;

        CountryCode = parcel.CountryCode;

        if (parcel is WbrParcel wbr)
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
        else if (parcel is OzonParcel ozon)
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

        StopWordIds = parcel.BaseParcelStopWords?
            .Select(bosw => bosw.StopWordId)
            .ToList() ?? [];
        KeyWordIds = parcel.BaseParcelKeyWords?
            .Select(bokw => bokw.KeyWordId)
            .ToList() ?? [];
        FeacnOrderIds = parcel.BaseParcelFeacnPrefixes?
            .Where(bofp => bofp.FeacnPrefix?.FeacnOrderId != null)
            .Select(bofp => bofp.FeacnPrefix.FeacnOrderId!.Value)
            .Distinct()
            .ToList() ?? [];
        FeacnPrefixIds = parcel.BaseParcelFeacnPrefixes?
            .Where(bofp => bofp.FeacnPrefix?.FeacnOrderId == null)
            .Select(bofp => bofp.FeacnPrefix.Id)
            .Distinct()
            .ToList() ?? [];
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}
