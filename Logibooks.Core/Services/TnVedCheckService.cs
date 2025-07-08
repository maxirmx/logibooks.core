using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class TnVedCheckService
{
    private readonly AppDbContext _db;
    private readonly Dictionary<string, HashSet<string>> _map = new();

    public TnVedCheckService(AppDbContext db)
    {
        _db = db;
        var items = _db.AltaItems.AsNoTracking().Select(i => i.Code).ToList();
        var exceptions = _db.AltaExceptions.AsNoTracking().Select(e => e.Code).ToList();
        foreach (var item in items)
        {
            var exSet = exceptions.Where(e => e.StartsWith(item)).ToHashSet();
            _map[item] = exSet;
        }
    }

    public async Task CheckOrder(int orderId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;
        string? tn = order.TnVed;
        int status = 201;
        if (!string.IsNullOrEmpty(tn))
        {
            foreach (var kv in _map)
            {
                if (tn.StartsWith(kv.Key))
                {
                    bool except = kv.Value.Any(ex => tn.StartsWith(ex));
                    status = except ? 201 : 101;
                    break;
                }
            }
        }
        order.StatusId = status;
        await _db.SaveChangesAsync();
    }
}
