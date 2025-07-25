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
using System.Text.RegularExpressions;

namespace Logibooks.Core.Services;

public class OrderValidationService(
    AppDbContext db, 
    IMorphologySearchService morphService, 
    IFeacnPrefixCheckService feacnPrefixCheckService) : IOrderValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IMorphologySearchService _morphService = morphService;
    private readonly IFeacnPrefixCheckService _feacnPrefixCheckService = feacnPrefixCheckService;
    private static readonly Regex TnVedRegex = new($"^\\d{{{FeacnPrefix.FeacnCodeLength}}}$", RegexOptions.Compiled);

    public async Task ValidateAsync(
        BaseOrder order,
        MorphologyContext morphologyContext,
        StopWordsContext stopWordsContext,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default)
    {
        // remove existing links for this order
        var existing1 = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderStopWord>().RemoveRange(existing1);

        var existing2 = _db.Set<BaseOrderFeacnPrefix>()
            .Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderFeacnPrefix>().RemoveRange(existing2);

        if (string.IsNullOrWhiteSpace(order.TnVed) || !TnVedRegex.IsMatch(order.TnVed))
        {
            order.CheckStatusId = (int)OrderCheckStatusCode.InvalidFeacnFormat; 
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.CheckStatusId = (int)OrderCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var productName = order.ProductName ?? string.Empty;
        var links1 = SelectStopWordLinks(order.Id, productName, stopWordsContext, morphologyContext);

        var links2 = feacnContext != null
            ? _feacnPrefixCheckService.CheckOrder(order, feacnContext)
            : await _feacnPrefixCheckService.CheckOrderAsync(order, cancellationToken);

        if (links1.Count > 0)
        {
            _db.AddRange(links1);
        }
        if (links2.Any())
        {
            _db.AddRange(links2);
        }
        if (links1.Count > 0 || links2.Any())
        {
            order.CheckStatusId = (int)OrderCheckStatusCode.HasIssues;
        }
        else
        {
            order.CheckStatusId = (int)OrderCheckStatusCode.NoIssues;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private List<BaseOrderStopWord> SelectStopWordLinks(
        int orderId,
        string productName,
        StopWordsContext stopWordsContext,
        MorphologyContext morphologyContext)
    {
        var links = new List<BaseOrderStopWord>();
        var existingStopWordIds = new HashSet<int>();

        List<StopWord> matchingWords = GetMatchingStopWordsFromContext(productName, stopWordsContext); 

        // Add stop words to links
        foreach (var sw in matchingWords)
        {
            links.Add(new BaseOrderStopWord { BaseOrderId = orderId, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        var ids = _morphService.CheckText(morphologyContext, productName);
        foreach (var id in ids)
        {
            if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                links.Add(new BaseOrderStopWord { BaseOrderId = orderId, StopWordId = id });
        }

        return links;
    }
    private static List<StopWord> GetMatchingStopWordsFromContext(string productName, StopWordsContext context)
    {
        if (string.IsNullOrEmpty(productName))
            return [];

        return context.ExactMatchStopWords
            .Where(sw => !string.IsNullOrEmpty(sw.Word) && 
                         productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public StopWordsContext InitializeStopWordsContext(IEnumerable<StopWord> exactMatchStopWords)
    {
        var context = new StopWordsContext();
        context.ExactMatchStopWords.AddRange(exactMatchStopWords.Where(sw => sw.ExactMatch));
        return context;
    }
}
