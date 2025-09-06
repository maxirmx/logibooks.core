// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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
