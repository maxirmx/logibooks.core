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

using System.Text.RegularExpressions;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class FeacnPrefixCheckService(AppDbContext db) : IFeacnPrefixCheckService
{
    private readonly AppDbContext _db = db;

    private static readonly Regex TnVedRegex = new("^\\d{10}$", RegexOptions.Compiled);

    public async Task CheckOrderAsync(BaseOrder order, CancellationToken cancellationToken = default)
    {
        // Step 1: remove existing links
        var existing = _db.Set<BaseOrderFeacnPrefix>()
            .Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderFeacnPrefix>().RemoveRange(existing);

        // Step 2: check TN VED format
        if (string.IsNullOrWhiteSpace(order.TnVed) || !TnVedRegex.IsMatch(order.TnVed))
        {
            order.CheckStatusId = 102; // Wrong TN VED format
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        string tnVed = order.TnVed!;

        // Step 3: load prefixes with exceptions
        var twoDigitPrefix = tnVed.Substring(0, 2);
        var prefixes = await _db.FeacnPrefixes
            .Where(p => p.Code.StartsWith(twoDigitPrefix))
            .Include(p => p.FeacnPrefixExceptions)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var links = new List<BaseOrderFeacnPrefix>();
        foreach (var prefix in prefixes)
        {
            if (MatchesPrefix(tnVed, prefix))
            {
                order.CheckStatusId = 101;
                links.Add(new BaseOrderFeacnPrefix
                {
                    BaseOrderId = order.Id,
                    FeacnPrefixId = prefix.Id
                });
            }
        }

        if (links.Count == 0)
        {
            order.CheckStatusId = (int)OrderCheckStatusCode.NoIssues;
        }
        else if (links.Count > 0)
        {
            _db.Set<BaseOrderFeacnPrefix>().AddRange(links);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool MatchesPrefix(string tnVed, FeacnPrefix prefix)
    {
        if (tnVed.Length < 2 || prefix.Code.Length < 2)
            return false;

        if (!tnVed.StartsWith(prefix.Code[..2]))
            return false;

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
