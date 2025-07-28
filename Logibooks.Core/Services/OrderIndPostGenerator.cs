using DocumentFormat.OpenXml.Bibliography;
using System.Text;
using System.IO.Compression;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Logibooks.Core.Services;

public class OrderIndPostGenerator(AppDbContext db, IIndPostXmlService xmlService) : IOrderIndPostGenerator
{
    private const string NotDefined = "не задано";
    private readonly AppDbContext _db = db;
    private readonly IIndPostXmlService _xmlService = xmlService;

    public async Task<(string, string)> GenerateXML(int orderId)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Country)
            .Include(o => o.Register)
                .ThenInclude(r => r.DestinationCountry)
            .Include(o => o.Register)
                .ThenInclude(r => r.TransportationType)
            .Include(o => o.Register)
                .ThenInclude(r => r.CustomsProcedure)
            .Include(o => o.Register)
                .ThenInclude(r => r.Company)
                    .ThenInclude(c => c!.Country)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order == null
            ? throw new InvalidOperationException($"Order not found [id={orderId}]")
            : (GenerateFilename(order), GenerateXML(order));
    }

    public string GenerateXML(BaseOrder order)
    {
        var register = order.Register!;

        var date = register.InvoiceDate.HasValue == true
                    ? register.InvoiceDate.Value.ToString("yyyy-MM-dd")
                    : NotDefined;
        var typeValue = register.TransportationType?.Code.ToString() ?? NotDefined;

        var originCountryCode = register.CustomsProcedure?.Code == 10
            ? "RU"
            : register.DestinationCountry?.IsoAlpha2 ?? NotDefined;
        
        var fields = new Dictionary<string, string?>
        {
            { "NUM", order.GetParcelNumber() },
            { "AVIANUM", register?.InvoiceNumber ?? NotDefined },
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
            { "SERVICE", "0" }
        };


        if (register?.CustomsProcedure?.Code == 10)
        {

            fields["CONSIGNEE_CHOICE"] = "1";
            fields["PERSONSURNAME"] = order.GetSurName();
            fields["PERSONNAME"] = order.GetName();
            fields["PERSONMIDDLENAME"] = order.GetMiddleName();
            fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] = register?.DestinationCountry?.IsoAlpha2 ?? NotDefined;
            fields["CONSIGNEE_ADDRESS_COUNRYNAME"] = register?.DestinationCountry?.NameRuShort ?? NotDefined;
            fields["CITY"] = order.GetCity();
            fields["STREETHOUSE"] = order.GetStreet();

            fields["CONSIGNOR_CHOICE"] = "2";
            fields["SENDER"] = register?.Company?.ShortName ?? NotDefined;
            fields["CONSIGNOR_RFORGANIZATIONFEATURES_KPP"] = register?.Company?.Kpp ?? NotDefined;
            fields["CONSIGNOR_RFORGANIZATIONFEATURES_INN"] = register?.Company?.Inn ?? NotDefined; 
            fields["CONSIGNOR_ADDRESS_POSTALCODE"] = register?.Company?.PostalCode ?? NotDefined;
            fields["CONSIGNOR_ADDRESS_CITY"] = register?.Company?.City ?? NotDefined;
            fields["CONSIGNOR_ADDRESS_STREETHOUSE"] = register?.Company?.Street ?? NotDefined;
            fields["COUNTRYCODE"] = register?.Company?.Country.IsoAlpha2 ?? NotDefined;
            fields["COUNTRYNAME"] = register?.Company?.Country.NameRuShort ?? NotDefined;
        }
        else if (register?.CustomsProcedure?.Code == 60)
        {
            fields["CONSIGNEE_CHOICE"] = "2";
            fields["CONSIGNEE_SHORTNAME"] = register?.Company?.ShortName ?? NotDefined;
            fields["CONSIGNEE_RFORGANIZATIONFEATURES_KPP"] = register?.Company?.Kpp ?? NotDefined;
            fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] = register?.Company?.Country.IsoAlpha2 ?? NotDefined;
            fields["CONSIGNEE_ADDRESS_COUNRYNAME"] = register?.Company?.Country.NameRuShort ?? NotDefined;
            fields["RFORGANIZATIONFEATURES_INN"] = register?.Company?.Inn ?? NotDefined;
            fields["CITY"] = register?.Company?.City ?? NotDefined;
            fields["STREETHOUSE"] = register?.Company?.Street ?? NotDefined;

            fields["CONSIGNOR_CHOICE"] = "1";
            fields["SENDER"] = order.GetFullName();
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDCODE"] = "10";
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDSERIES"] = order.GetSeries();
            fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDNUMBER"] = order.GetNumber();
            fields["CONSIGNOR_ADDRESS_CITY"] = order.GetCity();
            fields["CONSIGNOR_ADDRESS_STREETHOUSE"] = order.GetStreet();
            fields["COUNTRYCODE"] = register?.DestinationCountry?.IsoAlpha2 ?? NotDefined;
            fields["COUNTRYNAME"] = register?.DestinationCountry?.NameRuShort ?? NotDefined;

        }

        var goodsItems = new List<IDictionary<string, string?>>();

        IEnumerable<BaseOrder> ordersForGoods;
        if (order is OzonOrder ozonOrder)
        {
            ordersForGoods = _db.OzonOrders.AsNoTracking()
                .Where(o => o.PostingNumber == ozonOrder.PostingNumber && o.RegisterId == ozonOrder.RegisterId)
                .ToList<BaseOrder>();
        }
        else if (order is WbrOrder wbrOrder)
        {
            ordersForGoods = _db.WbrOrders.AsNoTracking()
                .Where(o => o.Shk == wbrOrder.Shk && o.RegisterId == wbrOrder.RegisterId)
                .ToList<BaseOrder>();
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

        fields["ALLWEIGHT"] = totalWeight.ToString(CultureInfo.InvariantCulture);
        fields["ALLCOST"] = totalCost.ToString(CultureInfo.InvariantCulture);
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
                .Include(o => o.Register).ThenInclude(r => r.DestinationCountry)
                .Include(o => o.Register).ThenInclude(r => r.TransportationType)
                .Include(o => o.Register).ThenInclude(r => r.CustomsProcedure)
                .Include(o => o.Register).ThenInclude(r => r.Company)
                .Where(o => o.RegisterId == registerId)
                .GroupBy(o => o.Shk)
                .Select(g => g.First())
                .Cast<BaseOrder>()
                .ToListAsync();
        }
        else if (register.CompanyId == IRegisterProcessingService.GetOzonId())
        {
            orders = await _db.OzonOrders.AsNoTracking()
                .Include(o => o.Country)
                .Include(o => o.Register).ThenInclude(r => r.DestinationCountry)
                .Include(o => o.Register).ThenInclude(r => r.TransportationType)
                .Include(o => o.Register).ThenInclude(r => r.CustomsProcedure)
                .Include(o => o.Register).ThenInclude(r => r.Company)
                .Where(o => o.RegisterId == registerId)
                .GroupBy(o => o.PostingNumber)
                .Select(g => g.First())
                .Cast<BaseOrder>()
                .ToListAsync();
        }
        else
        {
            orders = await _db.Orders.AsNoTracking()
                .Include(o => o.Country)
                .Include(o => o.Register).ThenInclude(r => r.DestinationCountry)
                .Include(o => o.Register).ThenInclude(r => r.TransportationType)
                .Include(o => o.Register).ThenInclude(r => r.CustomsProcedure)
                .Include(o => o.Register).ThenInclude(r => r.Company)
                .Where(o => o.RegisterId == registerId)
                .ToListAsync();
        }

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var order in orders)
            {
                var entry = archive.CreateEntry(GenerateFilename(order));
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                var xml = GenerateXML(order);
                writer.Write(xml);
            }
        }

        return ($"IndPost_{registerId}.zip", ms.ToArray());
    }
}
