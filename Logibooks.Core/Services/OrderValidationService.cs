using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class OrderValidationService(AppDbContext db) : IOrderValidationService
{
    private readonly AppDbContext _db = db;

    public async Task ValidateAsync(int orderId, CancellationToken cancellationToken = default)
    {
        // remove existing links for this order
        var existing = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == orderId);
        _db.Set<BaseOrderStopWord>().RemoveRange(existing);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order == null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }
        var description = order.Description ?? string.Empty;

        var words = await _db.StopWord.AsNoTracking()
            .Where(sw => sw.ExactMatch)
            .ToListAsync(cancellationToken);

        var links = new List<BaseOrderStopWord>();
        foreach (var sw in words)
        {
            if (!string.IsNullOrEmpty(sw.Word) &&
                description.Contains(sw.Word, StringComparison.OrdinalIgnoreCase))
            {
                links.Add(new BaseOrderStopWord { BaseOrderId = orderId, StopWordId = sw.Id });
            }
        }

        if (links.Count > 0)
        {
            _db.AddRange(links);
            order.CheckStatusId = 201;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
