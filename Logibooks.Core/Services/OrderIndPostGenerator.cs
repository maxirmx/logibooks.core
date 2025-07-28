using System.Text;
using System.IO.Compression;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

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

        var fields = new Dictionary<string, string?>
        {
            { "NUM", order.GetParcelNumber() },
            { "AVIANUM", register?.InvoiceNumber ?? NotDefined },
            { "AVIADATE", date },
            { "INVNUM", order.GetParcelNumber() },
            { "INVDATE", date },
            { "TYPE", typeValue },
            { "ARRIVEDATE", date },
            { "SERVICE", "0" },
        };

        var goodsItems = new List<IDictionary<string, string?>>();
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
