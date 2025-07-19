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
        MorphologyContext? morphologyContext = null,
        StopWordsContext? stopWordsContext = null,
        CancellationToken cancellationToken = default)
    {
        // remove existing links for this order
        var existing = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderStopWord>().RemoveRange(existing);
        order.CheckStatusId = (int)OrderCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var productName = order.ProductName ?? string.Empty;
        var links = await SelectStopWordLinksAsync(order.Id, productName, stopWordsContext, morphologyContext, cancellationToken);

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

    private async Task<List<BaseOrderStopWord>> SelectStopWordLinksAsync(
        int orderId,
        string productName,
        StopWordsContext? stopWordsContext,
        MorphologyContext? morphologyContext,
        CancellationToken cancellationToken)
    {
        var links = new List<BaseOrderStopWord>();
        var existingStopWordIds = new HashSet<int>();

        // Get matching stop words from context or database
        List<StopWord> matchingWords;
        if (stopWordsContext != null)
        {
            matchingWords = GetMatchingStopWordsFromContext(productName, stopWordsContext);
        }
        else
        {
            matchingWords = await GetMatchingStopWords(productName, cancellationToken);
        }

        // Add stop words to links
        foreach (var sw in matchingWords)
        {
            links.Add(new BaseOrderStopWord { BaseOrderId = orderId, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        // Add morphology-based matches
        if (morphologyContext != null)
        {
            var ids = _morphService.CheckText(morphologyContext, productName);
            foreach (var id in ids)
            {
                if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                    links.Add(new BaseOrderStopWord { BaseOrderId = orderId, StopWordId = id });
            }
        }

        return links;
    }
    private List<StopWord> GetMatchingStopWordsFromContext(string productName, StopWordsContext context)
    {
        if (string.IsNullOrEmpty(productName))
            return [];

        return context.ExactMatchStopWords
            .Where(sw => !string.IsNullOrEmpty(sw.Word) && 
                         productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<List<StopWord>> GetMatchingStopWords(string productName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(productName))
            return [];

        // Load all exact match stop words and filter in memory for case-insensitive matching
        var allWords = await _db.StopWords.AsNoTracking()
            .Where(sw => sw.ExactMatch && !string.IsNullOrEmpty(sw.Word))
            .ToListAsync(cancellationToken);

        return allWords.Where(sw => productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase))
                      .ToList();
    }

    public StopWordsContext InitializeStopWordsContext(IEnumerable<StopWord> exactMatchStopWords)
    {
        var context = new StopWordsContext();
        context.ExactMatchStopWords.AddRange(exactMatchStopWords.Where(sw => sw.ExactMatch));
        return context;
    }
}
