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
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE),
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using System.Linq;

namespace Logibooks.Core.Services;

public class ParcelFeacnCodeLookupService(
    AppDbContext db,
    IMorphologySearchService morphService) : IParcelFeacnCodeLookupService
{
    private readonly AppDbContext _db = db;
    private readonly IMorphologySearchService _morphService = morphService;

    public async Task<List<int>> LookupAsync(
        BaseParcel order,
        MorphologyContext morphologyContext,
        WordsLookupContext<KeyWord> wordsLookupContext,
        CancellationToken cancellationToken = default)
    {
        if (order.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return [];
        }

        var existing = _db.Set<BaseParcelKeyWord>().Where(l => l.BaseParcelId == order.Id);
        _db.Set<BaseParcelKeyWord>().RemoveRange(existing);

        var productName = order.ProductName ?? string.Empty;
        var links = SelectKeyWordLinks(order.Id, productName, wordsLookupContext, morphologyContext);

//        if (order is WbrOrder wbr && !string.IsNullOrWhiteSpace(wbr.Description))
//        {
//            var linksDesc = SelectKeyWordLinks(order.Id, wbr.Description, wordsLookupContext, morphologyContext);
//            var existingIds = new HashSet<int>(links.Select(l => l.KeyWordId));
//            foreach (var link in linksDesc)
//            {
//                if (existingIds.Add(link.KeyWordId))
//                {
//                    links.Add(link);
//                }
//            }
//        }

        if (links.Count > 0)
        {
            _db.AddRange(links);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return links.Select(l => l.KeyWordId).ToList();
    }

    private List<BaseParcelKeyWord> SelectKeyWordLinks(
        int orderId,
        string text,
        WordsLookupContext<KeyWord> wordsLookupContext,
        MorphologyContext morphologyContext)
    {
        var links = new List<BaseParcelKeyWord>();
        var existingKeyWordIds = new HashSet<int>();

        var matchingWords = wordsLookupContext.GetMatchingWords(text);

        foreach (var kw in matchingWords)
        {
            links.Add(new BaseParcelKeyWord { BaseParcelId = orderId, KeyWordId = kw.Id });
            existingKeyWordIds.Add(kw.Id);
        }

        var ids = _morphService.CheckText(morphologyContext, text);
        foreach (var id in ids)
        {
            if (existingKeyWordIds.Add(id))
            {
                links.Add(new BaseParcelKeyWord { BaseParcelId = orderId, KeyWordId = id });
            }
        }

        return links;
    }
}
