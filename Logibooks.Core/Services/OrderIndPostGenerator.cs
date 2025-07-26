using System.Text;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class OrderIndPostGenerator(AppDbContext db, IIndPostXmlService xmlService) : IOrderIndPostGenerator
{
    private readonly AppDbContext _db = db;
    private readonly IIndPostXmlService _xmlService = xmlService;

    public async Task<string> GenerateXML(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"Order not found [id={orderId}]");
        }
        return await GenerateXML(order);
    }

    public Task<string> GenerateXML(BaseOrder order)
    {
        var fields = new Dictionary<string, string?>();
        var goodsItems = new List<IDictionary<string, string?>>();

        var xml = _xmlService.CreateXml(fields, goodsItems);
        return Task.FromResult(xml);
    }

    public async Task<string> GenerateFilename(int orderId)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new InvalidOperationException($"Order not found [id={orderId}]");
        }
        return GenerateFilename(order);
    }

    public string GenerateFilename(BaseOrder order)
    {
        if (order is WbrOrder wbr)
        {
            var shk = wbr.Shk ?? string.Empty;
            return $"IndPost_{shk.PadLeft(20, '0')}.xml";
        }
        else if (order is OzonOrder ozon)
        {
            var ozonId = ozon.OzonId ?? string.Empty;
            return $"IndPost_{ozonId}.xml";
        }
        else
        {
            return "IndPost_.xml";
        }
    }
}
