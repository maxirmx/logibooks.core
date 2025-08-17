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

using Microsoft.EntityFrameworkCore;

using System.Globalization;
using System.IO.Compression;
using System.Text;

using Logibooks.Core.Constants;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Services;

public class ParcelIndPostGenerator(AppDbContext db, IIndPostXmlService xmlService) : IParcelIndPostGenerator
{
    private readonly AppDbContext _db = db;
    private readonly IIndPostXmlService _xmlService = xmlService;

    public async Task<(string, string)> GenerateXML(int orderId)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Country)
            .Include(o => o.Register)
                .ThenInclude(r => r.TheOtherCountry)
            .Include(o => o.Register)
                .ThenInclude(r => r.TransportationType)
            .Include(o => o.Register)
                .ThenInclude(r => r.CustomsProcedure)
            .Include(o => o.Register)
                .ThenInclude(r => r.Company)
                    .ThenInclude(c => c!.Country)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new InvalidOperationException($"Order not found [id={orderId}]");

        // Skip if in [HasIssues, NoIssues)
        if (order.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues && order.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues)
            throw new InvalidOperationException($"Order is not eligible for IndPost XML [id={orderId}]");

        return (GenerateFilename(order), GenerateXML(order));
    }

    public string GenerateXML(BaseOrder order)
    {
        // Skip if in [HasIssues, NoIssues)
        if (order.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues && order.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues)
            throw new InvalidOperationException($"Order is not eligible for IndPost XML [id={order.Id}]");

        var register = order.Register!;

        var date = register.InvoiceDate.HasValue == true
                    ? register.InvoiceDate.Value.ToString("yyyy-MM-dd")
                    : Placeholders.NotSet;

        var typeValue = register.TransportationType != null
                        ? ((int)register.TransportationType.Code).ToString()
                        : Placeholders.NotSet;

        var originCountryCode = register.CustomsProcedure?.Code == 10
            ? "RU"
            : register.TheOtherCountry?.IsoAlpha2 ?? Placeholders.NotSet;
        
        var fields = new Dictionary<string, string?>
        {
            { "NUM", order.GetParcelNumber() },
            { "AVIANUM", register?.InvoiceNumber ?? Placeholders.NotSet },
            { "AVIADATE", date },
            { "INVNUM", order.GetParcelNumber() },
            { "INVDATE", date },
            { "TYPE", typeValue },
            { "ARRIVEDATE", date },
            { "CURRENCY", order.GetCurrency() },
            { "ORGCOUNTRY", originCountryCode },
            { "DELIVERYTERMS_TRADINGCOUNTRYCODE", originCountryCode },
            { "DELIVERYTERMS_DISPATCHCOUNTRYCODE", originCountryCode },
            { "DELIVERYTERMS_DELIVERYTERMSSTRINGCODE", "CPT" },
            { "SERVICE", "0" },
            { "PLACES", "1" }
        };


        if (register?.CustomsProcedure?.Code == 10)
        {

            fields["CONSIGNEE_CHOICE"] = "1";
            fields["PERSONSURNAME"] = order.GetSurName();
            fields["PERSONNAME"] = order.GetName();
            fields["PERSONMIDDLENAME"] = order.GetMiddleName();
            fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] = SetOrDefault(register?.TheOtherCountry?.IsoAlpha2);
            // CONSIGNEE_ADDRESS_COUNRYNAME так в схеме
            fields["CONSIGNEE_ADDRESS_COUNRYNAME"] = SetOrDefault(register?.TheOtherCountry?.NameRuShort);
            fields["CONSIGNEE_IDENTITYCARD_IDENTITYCARDCODE"] = "10";
            fields["CONSIGNEE_IDENTITYCARD_IDENTITYCARDSERIES"] = order.GetSeries();
            fields["CONSIGNEE_IDENTITYCARD_IDENTITYCARDNUMBER"] = order.GetNumber();
            fields["CONSIGNEE_IDENTITYCARD_COUNTRYCODE"] = SetOrDefault(register?.TheOtherCountry?.IsoAlpha2);
            fields["CITY"] = order.GetCity();
            fields["STREETHOUSE"] = order.GetStreet();

            fields["CONSIGNOR_CHOICE"] = "2";
            fields["SENDER"] = SetOrDefault(register?.Company?.ShortName);
            fields["CONSIGNOR_RFORGANIZATIONFEATURES_KPP"] = SetOrDefault(register?.Company?.Kpp);
            fields["CONSIGNOR_RFORGANIZATIONFEATURES_OGRN"] = SetOrDefault(register?.Company?.Ogrn);
            fields["CONSIGNOR_RFORGANIZATIONFEATURES_INN"] = SetOrDefault(register?.Company?.Inn);
            fields["CONSIGNOR_ADDRESS_POSTALCODE"] = SetOrDefault(register?.Company?.PostalCode);
            fields["CONSIGNOR_ADDRESS_CITY"] = SetOrDefault(register?.Company?.City);
            fields["CONSIGNOR_ADDRESS_STREETHOUSE"] = SetOrDefault(register?.Company?.Street);
            fields["COUNTRYCODE"] = SetOrDefault(register?.Company?.Country.IsoAlpha2);
            fields["COUNTRYNAME"] = SetOrDefault(register?.Company?.Country.NameRuShort);
        }
        else if (register?.CustomsProcedure?.Code == 60)
        {
            fields["CONSIGNEE_CHOICE"] = "2";
            fields["CONSIGNEE_SHORTNAME"] = SetOrDefault(register?.Company?.ShortName);
            fields["CONSIGNEE_RFORGANIZATIONFEATURES_KPP"] = SetOrDefault(register?.Company?.Kpp);
            fields["CONSIGNEE_RFORGANIZATIONFEATURES_OGRN"] = SetOrDefault(register?.Company?.Ogrn);
            fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] = SetOrDefault(register?.Company?.Country.IsoAlpha2);
            // CONSIGNEE_ADDRESS_COUNRYNAME  так в схеме
            fields["CONSIGNEE_ADDRESS_COUNRYNAME"] = SetOrDefault(register?.Company?.Country.NameRuShort);
            fields["RFORGANIZATIONFEATURES_INN"] = SetOrDefault(register?.Company?.Inn);
            fields["CITY"] = SetOrDefault(register?.Company?.City);
            fields["STREETHOUSE"] = SetOrDefault(register?.Company?.Street);

            fields["CONSIGNOR_CHOICE"] = "1";
            fields["SENDER"] = order.GetFullName();
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDCODE"] = "10";
            fields["CONSIGNOR_IDENTITYCARD_FULLIDENTITYCARDNAME"] = "Иностранный паспорт";
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDSERIES"] = order.GetSeries();
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDNUMBER"] = order.GetNumber();
            fields["CONSIGNOR_IDENTITYCARD_COUNTRYCODE"] = SetOrDefault(register?.TheOtherCountry?.IsoAlpha2);
            fields["CONSIGNOR_ADDRESS_CITY"] = order.GetCity();
            fields["CONSIGNOR_ADDRESS_STREETHOUSE"] = order.GetStreet();
            fields["COUNTRYCODE"] = SetOrDefault(register?.TheOtherCountry?.IsoAlpha2);
            fields["COUNTRYNAME"] = SetOrDefault(register?.TheOtherCountry?.NameRuShort);

        }

        var goodsItems = new List<IDictionary<string, string?>>();

        IEnumerable<BaseOrder> ordersForGoods;
        if (order is OzonOrder ozonOrder)
        {
            ordersForGoods = 
                [.. _db.OzonOrders.AsNoTracking().Where(o => o.PostingNumber == ozonOrder.PostingNumber && 
                                                        o.RegisterId == ozonOrder.RegisterId)];
        }
        else if (order is WbrOrder wbrOrder)
        {
            ordersForGoods = 
                [.. _db.WbrOrders.AsNoTracking().Where(o => o.Shk == wbrOrder.Shk && 
                                                                         o.RegisterId == wbrOrder.RegisterId)];
        }
        else
        {
            ordersForGoods = [];
        }

        decimal totalCost = 0m;
        decimal totalWeight = 0m;

        foreach (var o in ordersForGoods)
        {
            goodsItems.Add(new Dictionary<string, string?>
            {
                { "DESCR", o.GetDescription() },
                { "QTY", o.GetQuantity() },
                { "COST", o.GetCost() },
                { "COSTRUB", o.GetCost() },
                { "WEIGHT", o.GetWeight() },
                { "URL", o.GetUrl() },
                { "TNVED", o.GetTnVed() }
            });

            if (decimal.TryParse(o.GetCost(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost))
                totalCost += cost;
            if (decimal.TryParse(o.GetWeight(), NumberStyles.Any, CultureInfo.InvariantCulture, out var weight))
                totalWeight += weight;
        }

        fields["ALLWEIGHT"] = BaseOrder.FormatWeight(totalWeight);
        fields["ALLCOST"] = BaseOrder.FormatCost(totalCost);
        var xml = _xmlService.CreateXml(fields, goodsItems);
        return xml;
    }

    public string GenerateFilename(BaseOrder order) => $"IndPost_{order.GetParcelNumber()}.xml";

    public async Task<(string, byte[])> GenerateXML4R(int registerId)
    {
        var register = await _db.Registers.AsNoTracking()
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.Id == registerId);
        if (register == null)
        {
            throw new InvalidOperationException($"Register not found [id={registerId}]");
        }

        List<BaseOrder> orders;
        if (register.CompanyId == IRegisterProcessingService.GetWBRId())
        {
            orders = await _db.WbrOrders.AsNoTracking()
                .Include(o => o.Country)
                .Include(o => o.Register).ThenInclude(r => r.TheOtherCountry)
                .Include(o => o.Register).ThenInclude(r => r.TransportationType)
                .Include(o => o.Register).ThenInclude(r => r.CustomsProcedure)
                .Include(o => o.Register).ThenInclude(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .Where(o => o.RegisterId == registerId && !(o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues && o.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues))
                .GroupBy(o => o.Shk)
                .Select(g => g.First())
                .Cast<BaseOrder>()
                .ToListAsync();
        }
        else if (register.CompanyId == IRegisterProcessingService.GetOzonId())
        {
            orders = await _db.OzonOrders.AsNoTracking()
                .Include(o => o.Country)
                .Include(o => o.Register).ThenInclude(r => r.TheOtherCountry)
                .Include(o => o.Register).ThenInclude(r => r.TransportationType)
                .Include(o => o.Register).ThenInclude(r => r.CustomsProcedure)
                .Include(o => o.Register).ThenInclude(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .Where(o => o.RegisterId == registerId && !(o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues && o.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues))
                .GroupBy(o => o.PostingNumber)
                .Select(g => g.First())
                .Cast<BaseOrder>()
                .ToListAsync();
        }
        else
        {
            // If register is neither Ozon nor WBR, return empty zip file
            orders = [];
        }

        var fileBase = !string.IsNullOrWhiteSpace(register.InvoiceNumber) ? register.InvoiceNumber : registerId.ToString();

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var order in orders)
            {
                // Already filtered, but double check
                if (order.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues && order.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues)
                    continue;

                var entry = archive.CreateEntry($"{fileBase}_{order.GetParcelNumber()}.xml");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                var xml = GenerateXML(order);
                writer.Write(xml);
            }
        }

        return ($"IndPost_{fileBase}.zip", ms.ToArray());
    }

    private static string SetOrDefault(string? s) => s ?? Placeholders.NotSet;
}
