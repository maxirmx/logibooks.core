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
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Logibooks.Core.Services;

public class ParcelValidationService(
    AppDbContext db, 
    IMorphologySearchService morphService, 
    IFeacnPrefixCheckService feacnPrefixCheckService) : IParcelValidationService
{
    private readonly AppDbContext _db = db;
    private readonly IMorphologySearchService _morphService = morphService;
    private readonly IFeacnPrefixCheckService _feacnPrefixCheckService = feacnPrefixCheckService;
    private static readonly Regex TnVedRegex = new($"^\\d{{{FeacnCode.FeacnCodeLength}}}$", RegexOptions.Compiled);

    public async Task ValidateKwAsync(
        BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<StopWord> wordsLookupContext,
        CancellationToken cancellationToken = default)
    {
        if (order.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return;
        }

        var existing = _db.Set<BaseParcelStopWord>().Where(l => l.BaseParcelId == order.Id);
        _db.Set<BaseParcelStopWord>().RemoveRange(existing);

        var productName = order.ProductName ?? string.Empty;
        var links = SelectStopWordLinks(order.Id, productName, wordsLookupContext, morphologyContext);

        if (order is WbrParcel wbr && !string.IsNullOrWhiteSpace(wbr.Description))
        {
            var linksDesc = SelectStopWordLinks(order.Id, wbr.Description, wordsLookupContext, morphologyContext);
            var existingIds = new HashSet<int>(links.Select(l => l.StopWordId));
            foreach (var link in linksDesc)
            {
                if (existingIds.Add(link.StopWordId))
                {
                    links.Add(link);
                }
            }
        }

        if (links.Count > 0)
        {
            _db.AddRange(links);
            order.CheckStatusId = (int)ParcelCheckStatusCode.HasIssues;
        }
        else if (order.CheckStatusId == (int)ParcelCheckStatusCode.NotChecked ||
                 order.CheckStatusId == 0 ||
                 order.CheckStatusId == (int)ParcelCheckStatusCode.NoIssues)
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.NoIssues;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ValidateFeacnAsync(
        BaseParcel order,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default)
    {
        if (order.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return;
        }

        var existing = _db.Set<BaseParcelFeacnPrefix>().Where(l => l.BaseParcelId == order.Id);
        _db.Set<BaseParcelFeacnPrefix>().RemoveRange(existing);

        if (string.IsNullOrWhiteSpace(order.TnVed) || !TnVedRegex.IsMatch(order.TnVed))
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.InvalidFeacnFormat;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.CheckStatusId = (int)ParcelCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var links = feacnContext != null
            ? _feacnPrefixCheckService.CheckParcel(order, feacnContext)
            : await _feacnPrefixCheckService.CheckParcelAsync(order, cancellationToken);

        if (links.Any())
        {
            _db.AddRange(links);
            order.CheckStatusId = (int)ParcelCheckStatusCode.HasIssues;
        }
        else if (order.CheckStatusId == (int)ParcelCheckStatusCode.NotChecked ||
                 order.CheckStatusId == 0 ||
                 order.CheckStatusId == (int)ParcelCheckStatusCode.NoIssues)
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.NoIssues;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private List<BaseParcelStopWord> SelectStopWordLinks(
        int orderId,
        string productName,
        WordsLookupContext<StopWord> wordsLookupContext,
        MorphologyContext morphologyContext)
    {
        var links = new List<BaseParcelStopWord>();
        var existingStopWordIds = new HashSet<int>();

        var matchingWords = wordsLookupContext.GetMatchingWords(productName);

        // Add stop words to links
        foreach (var sw in matchingWords)
        {
            links.Add(new BaseParcelStopWord { BaseParcelId = orderId, StopWordId = sw.Id });
            existingStopWordIds.Add(sw.Id);
        }

        var ids = _morphService.CheckText(morphologyContext, productName);
        foreach (var id in ids)
        {
            if (existingStopWordIds.Add(id)) // HashSet.Add returns false if already exists
                links.Add(new BaseParcelStopWord { BaseParcelId = orderId, StopWordId = id });
        }

        return links;
    }
}
