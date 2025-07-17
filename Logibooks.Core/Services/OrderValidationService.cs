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

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class OrderValidationService(AppDbContext db, IMorphologySearchService morphService) : IOrderValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IMorphologySearchService _morphService = morphService;

    public async Task ValidateAsync(
        BaseOrder order,
        MorphologyContext? context = null,
        CancellationToken cancellationToken = default)
    {
        // remove existing links for this order
        var existing = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderStopWord>().RemoveRange(existing);
        order.CheckStatusId = (int)OrderCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var productName = order.ProductName ?? string.Empty;

        // Use database-appropriate case-insensitive matching
        var matchingWords = await GetMatchingStopWords(productName, cancellationToken);

        var links = new List<BaseOrderStopWord>();
        var existingStopWordIds = new HashSet<int>();

        foreach (var sw in matchingWords)
        {
            links.Add(new BaseOrderStopWord { BaseOrderId = order.Id, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        if (context != null)
        {
            var ids = _morphService.CheckText(context, productName);
            foreach (var id in ids)
            {
                if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                    links.Add(new BaseOrderStopWord { BaseOrderId = order.Id, StopWordId = id });
            }
        }

        if (links.Count > 0)
        {
            _db.AddRange(links);
            order.CheckStatusId = (int)OrderCheckStatusCode.HasIssues;
        }
        else
        {
            order.CheckStatusId = (int)OrderCheckStatusCode.NoIssues; 
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<StopWord>> GetMatchingStopWords(string productName, CancellationToken cancellationToken)
    {
        var isPostgreSQL = _db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isPostgreSQL)
        {
            // Use PostgreSQL's case-insensitive ILike for optimal performance
            return await _db.StopWords.AsNoTracking()
                .Where(sw => sw.ExactMatch && !string.IsNullOrEmpty(sw.Word) && 
                             EF.Functions.ILike(sw.Word, $"%{productName}%"))
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Fallback for other providers (like InMemory for tests)
            // Load all exact match stop words and filter in memory for case-insensitive matching
            var allWords = await _db.StopWords.AsNoTracking()
                .Where(sw => sw.ExactMatch && !string.IsNullOrEmpty(sw.Word))
                .ToListAsync(cancellationToken);

            return allWords.Where(sw => productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase))
                          .ToList();
        }
    }
}
