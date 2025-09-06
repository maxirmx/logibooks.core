// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class FeacnPrefixCheckService(AppDbContext db) : IFeacnPrefixCheckService
{
    private readonly AppDbContext _db = db;

    public async Task<IEnumerable<BaseParcelFeacnPrefix>> CheckParcelAsync(BaseParcel parcel, CancellationToken cancellationToken = default)
    {
        if (parcel.TnVed == null || parcel.TnVed.Length < 2)
        {
            return [];
        }

        string tnVed = parcel.TnVed;
        var twoDigitPrefix = tnVed[..2];
        
        var prefixes = await _db.FeacnPrefixes
            .Where(p => p.Code.StartsWith(twoDigitPrefix) && 
             ((p.FeacnOrder != null && p.FeacnOrder.Enabled) || p.FeacnOrder == null))
            .Include(p => p.FeacnPrefixExceptions)
            .Include(p => p.FeacnOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var links = new List<BaseParcelFeacnPrefix>();
        foreach (var prefix in prefixes)
        {
            if (MatchesPrefix(tnVed, prefix))
            {
                links.Add(new BaseParcelFeacnPrefix
                {
                    BaseParcelId = parcel.Id,
                    FeacnPrefixId = prefix.Id
                });
            }
        }

        return links;
    }

    public IEnumerable<BaseParcelFeacnPrefix> CheckParcel(BaseParcel parcel, FeacnPrefixCheckContext context)
    {
        if (parcel.TnVed == null || parcel.TnVed.Length < 2)
        {
            return [];
        }

        string tnVed = parcel.TnVed;
        var twoDigitPrefix = tnVed[..2];

        if (!context.Prefixes.TryGetValue(twoDigitPrefix, out var prefixes))
        {
            return [];
        }

        var links = new List<BaseParcelFeacnPrefix>();
        foreach (var prefix in prefixes)
        {
            if (MatchesPrefix(tnVed, prefix))
            {
                links.Add(new BaseParcelFeacnPrefix
                {
                    BaseParcelId = parcel.Id,
                    FeacnPrefixId = prefix.Id
                });
            }
        }

        return links;
    }

    public async Task<FeacnPrefixCheckContext> CreateContext(CancellationToken cancellationToken = default)
    {
        var prefixes = await _db.FeacnPrefixes
            .Where(p => !string.IsNullOrEmpty(p.Code) && p.Code.Length >= 2 && 
                ((p.FeacnOrder != null && p.FeacnOrder.Enabled) || p.FeacnOrder == null))
            .Include(p => p.FeacnOrder)
            .Include(p => p.FeacnPrefixExceptions)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var context = new FeacnPrefixCheckContext();
        foreach (var prefix in prefixes)
        {
            var key = prefix.Code[..2];
            if (!context.Prefixes.TryGetValue(key, out var list))
            {
                list = [];
                context.Prefixes[key] = list;
            }
            list.Add(prefix);
        }

        return context;
    }
    private static bool MatchesPrefix(string tnVed, FeacnPrefix prefix)
    {
        if (prefix.LeftValue != 0 && prefix.RightValue != 0)
        {
            if (!long.TryParse(tnVed, out var value))
                return false;

            if (value < prefix.LeftValue || value > prefix.RightValue)
                return false;
        }
        else if (!tnVed.StartsWith(prefix.Code))
        {
            return false;
        }

        foreach (var exc in prefix.FeacnPrefixExceptions)
        {
            if (!string.IsNullOrEmpty(exc.Code) && tnVed.StartsWith(exc.Code))
                return false;
        }

        return true;
    }
}
