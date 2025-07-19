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
        FeacnPrefixCheckContext? feacnPrefixContext = null,
        CancellationToken cancellationToken = default)
    {
        // remove existing links for this order
        var existingSw = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == order.Id);
        var existingPrefixes = _db.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderStopWord>().RemoveRange(existingSw);
        _db.Set<BaseOrderFeacnPrefix>().RemoveRange(existingPrefixes);
        order.CheckStatusId = (int)OrderCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var productName = order.ProductName ?? string.Empty;

        // Use pre-loaded stop words from context or load from database
        List<StopWord> matchingWords;
        if (stopWordsContext != null)
        {
            matchingWords = GetMatchingStopWordsFromContext(productName, stopWordsContext);
        }
        else
        {
            matchingWords = await GetMatchingStopWords(productName, cancellationToken);
        }

        var links = new List<BaseOrderStopWord>();
        var existingStopWordIds = new HashSet<int>();

        foreach (var sw in matchingWords)
        {
            links.Add(new BaseOrderStopWord { BaseOrderId = order.Id, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        if (morphologyContext != null)
        {
            var ids = _morphService.CheckText(morphologyContext, productName);
            foreach (var id in ids)
            {
                if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                    links.Add(new BaseOrderStopWord { BaseOrderId = order.Id, StopWordId = id });
            }
        }

        var prefixLinks = feacnPrefixContext != null
            ? await CheckOrderWithContextAsync(order, feacnPrefixContext, cancellationToken)
            : await CheckOrderAsync(order, cancellationToken);

        if (links.Count > 0)
            _db.AddRange(links);
        if (prefixLinks.Count > 0)
            _db.AddRange(prefixLinks);

        if (links.Count > 0 || prefixLinks.Count > 0)
            order.CheckStatusId = (int)OrderCheckStatusCode.HasIssues;
        else
            order.CheckStatusId = (int)OrderCheckStatusCode.NoIssues;

        await _db.SaveChangesAsync(cancellationToken);
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

    public FeacnPrefixCheckContext InitializeFeacnPrefixCheckContext(IEnumerable<FeacnPrefix> prefixes)
    {
        var context = new FeacnPrefixCheckContext();
        context.Prefixes.AddRange(prefixes);
        return context;
    }

    private async Task<List<BaseOrderFeacnPrefix>> CheckOrderAsync(BaseOrder order, CancellationToken cancellationToken)
    {
        var prefixes = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .ToListAsync(cancellationToken);
        var context = InitializeFeacnPrefixCheckContext(prefixes);
        return CheckOrderWithContext(order, context);
    }

    private Task<List<BaseOrderFeacnPrefix>> CheckOrderWithContextAsync(BaseOrder order, FeacnPrefixCheckContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(CheckOrderWithContext(order, context));
    }

    private List<BaseOrderFeacnPrefix> CheckOrderWithContext(BaseOrder order, FeacnPrefixCheckContext context)
    {
        var result = new List<BaseOrderFeacnPrefix>();
        var tnVed = order.TnVed ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tnVed))
            return result;

        foreach (var prefix in context.Prefixes)
        {
            if (PrefixMatches(tnVed, prefix))
            {
                result.Add(new BaseOrderFeacnPrefix { BaseOrderId = order.Id, FeacnPrefixId = prefix.Id });
            }
        }

        return result;
    }

    private static bool PrefixMatches(string tnVed, FeacnPrefix prefix)
    {
        if (string.IsNullOrWhiteSpace(tnVed) || string.IsNullOrWhiteSpace(prefix.Code))
            return false;

        var code = prefix.Code;
        if (prefix.IntervalCode == null)
        {
            if (!tnVed.StartsWith(code))
                return false;
        }
        else
        {
            var len = code.Length;
            if (tnVed.Length < len)
                return false;
            var value = tnVed[..len];
            if (string.Compare(value, code, StringComparison.Ordinal) < 0 ||
                string.Compare(value, prefix.IntervalCode, StringComparison.Ordinal) > 0)
                return false;
        }

        if (prefix.FeacnPrefixExceptions != null)
        {
            foreach (var exc in prefix.FeacnPrefixExceptions)
            {
                if (!string.IsNullOrWhiteSpace(exc.Code) && tnVed.StartsWith(exc.Code))
                    return false;
            }
        }

        return true;
    }
}
