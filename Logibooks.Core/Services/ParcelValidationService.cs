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
    private static readonly Regex TnVedRegex = new($"^\\d{{{FeacnPrefix.FeacnCodeLength}}}$", RegexOptions.Compiled);

    public async Task ValidateAsync(
        BaseOrder order,
        MorphologyContext morphologyContext,
        WordsLookupContext wordsLookupContext,
        FeacnPrefixCheckContext? feacnContext = null,
        CancellationToken cancellationToken = default)
    {
        if (order.CheckStatusId == (int)ParcelCheckStatusCode.MarkedByPartner)
        {
            return;
        }

        // remove existing links for this order
        var existing1 = _db.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderStopWord>().RemoveRange(existing1);

        var existing2 = _db.Set<BaseOrderFeacnPrefix>()
            .Where(l => l.BaseOrderId == order.Id);
        _db.Set<BaseOrderFeacnPrefix>().RemoveRange(existing2);

        if (string.IsNullOrWhiteSpace(order.TnVed) || !TnVedRegex.IsMatch(order.TnVed))
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.InvalidFeacnFormat;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.CheckStatusId = (int)ParcelCheckStatusCode.NotChecked;
        await _db.SaveChangesAsync(cancellationToken);

        var productName = order.ProductName ?? string.Empty;
        var links1 = SelectStopWordLinks(order.Id, productName, wordsLookupContext, morphologyContext);

        if (order is WbrOrder wbr && !string.IsNullOrWhiteSpace(wbr.Description))
        {
            var linksDesc = SelectStopWordLinks(order.Id, wbr.Description, wordsLookupContext, morphologyContext);
            // Add linksDesc to links1, keeping uniqueness by StopWordId
            var existingIds = new HashSet<int>(links1.Select(l => l.StopWordId));
            foreach (var link in linksDesc)
            {
                if (existingIds.Add(link.StopWordId))
                {
                    links1.Add(link);
                }
            }
        }

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
        // If stopwords found in productName or description, or feacn links found, set HasIssues
        if (links1.Count > 0 || links2.Any())
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.HasIssues;
        }
        else
        {
            order.CheckStatusId = (int)ParcelCheckStatusCode.NoIssues;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private List<BaseOrderStopWord> SelectStopWordLinks(
        int orderId,
        string productName,
        WordsLookupContext wordsLookupContext,
        MorphologyContext morphologyContext)
    {
        var links = new List<BaseOrderStopWord>();
        var existingStopWordIds = new HashSet<int>();

        List<StopWord> matchingWords = GetMatchingStopWordsFromContext(productName, wordsLookupContext); 

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

    private static List<StopWord> GetMatchingStopWordsFromContext(string productName, WordsLookupContext context)
    {
        var result = new List<StopWord>();
        // ExactSymbolsMatchItems: substring match
        result.AddRange(context.ExactSymbolsMatchItems
            .Where(sw => !string.IsNullOrEmpty(sw.Word) && 
                         productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase)));

        // ExactWordMatchItems: word match (delimited by non-alphanumeric or '-')
        foreach (var pair in context.ExactWordRegexes)
        {
            var sw = pair.sw;
            var regex = pair.regex;
            if (regex.IsMatch(productName))
                result.Add(sw);
        }

        // PhraseMatchItems: phrase match (sequence of words in exact order, separated by non-word chars)
        foreach (var pair in context.PhraseRegexes)
        {
            var sw = pair.sw;
            var regex = pair.regex;
            if (regex.IsMatch(productName))
                result.Add(sw);
        }

        return result;
    }

    public WordsLookupContext InitializeWordsLookupContext(IEnumerable<StopWord> exactMatchStopWords)
    {
        var context = new WordsLookupContext();
        List<StopWord> exactWordMatchItems  = [];
        List<StopWord> allWordsMatchItems = [];
        List<StopWord> phraseMatchItems = [];

        var filtered = exactMatchStopWords
            .Where(sw => sw.MatchTypeId < (int)WordMatchTypeCode.MorphologyMatchTypes);

        var grouped = filtered
            .GroupBy(sw => (WordMatchTypeCode)sw.MatchTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        context.ExactSymbolsMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.ExactSymbols, out var symbols) ? symbols : []);
        exactWordMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.ExactWord, out var words) ? words : []);
        phraseMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.Phrase, out var phrases) ? phrases : []);

        // Precompile regexes for ExactWordMatchItems
        foreach (var sw in exactWordMatchItems)
        {
            if (!string.IsNullOrEmpty(sw.Word))
            {
                var regex = new Regex($@"(?<=^|[^\w-]){Regex.Escape(sw.Word.Trim())}(?=[^\w-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                context.ExactWordRegexes.Add((sw, regex));
            }
        }

        // Precompile regexes for PhraseMatchItems
        foreach (var sw in phraseMatchItems)
        {
            if (!string.IsNullOrWhiteSpace(sw.Word))
            {
                // Split phrase into words (alphanumeric or '-') using regex
                var phraseWords = Regex.Split(sw.Word.Trim(), "[^\\w-]+", RegexOptions.Compiled)
                    .Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
                if (phraseWords.Length > 0)
                {
                    // Build regex for exact sequence of words, separated by non-word chars
                    var pattern = string.Join("[^\\w-]+", phraseWords.Select(w => $"{Regex.Escape(w)}"));
                    // Match phrase at word boundaries (start/end or non-word char)
                    var phraseRegex = new Regex(@$"(?<=^|[^\w-]){pattern}(?=[^\w-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    context.PhraseRegexes.Add((sw, phraseRegex));
                }
            }
        }

        return context;
    }
}
