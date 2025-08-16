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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Logibooks.Core.Models;

public class WordsLookupContext<TWord> where TWord : WordBase
{
    internal List<TWord> ExactSymbolsMatchItems { get; } = [];
    internal List<(TWord word, Regex regex)> ExactWordRegexes { get; } = [];
    internal List<(TWord word, Regex regex)> PhraseRegexes { get; } = [];

    public WordsLookupContext(IEnumerable<TWord> words)
    {
        List<TWord> exactWordMatchItems = [];
        List<TWord> phraseMatchItems = [];

        var filtered = words
            .Where(sw => sw.MatchTypeId < (int)WordMatchTypeCode.MorphologyMatchTypes);

        var grouped = filtered
            .GroupBy(sw => (WordMatchTypeCode)sw.MatchTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        ExactSymbolsMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.ExactSymbols, out var symbols) ? symbols : []);
        exactWordMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.ExactWord, out var wordsGroup) ? wordsGroup : []);
        phraseMatchItems.AddRange(
            grouped.TryGetValue(WordMatchTypeCode.Phrase, out var phrases) ? phrases : []);

        foreach (var sw in exactWordMatchItems)
        {
            if (!string.IsNullOrEmpty(sw.Word))
            {
                var regex = new Regex($@"(?<=^|[^\w-]){Regex.Escape(sw.Word.Trim())}(?=[^\w-]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);
                ExactWordRegexes.Add((sw, regex));
            }
        }

        foreach (var sw in phraseMatchItems)
        {
            if (!string.IsNullOrWhiteSpace(sw.Word))
            {
                var phraseWords = Regex.Split(sw.Word.Trim(), "[^\\w-]+", RegexOptions.Compiled)
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .ToArray();
                if (phraseWords.Length > 0)
                {
                    var pattern = string.Join("[^\\w-]+", phraseWords.Select(w => $"{Regex.Escape(w)}"));
                    var phraseRegex = new Regex(@$"(?<=^|[^\w-]){pattern}(?=[^\w-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    PhraseRegexes.Add((sw, phraseRegex));
                }
            }
        }
    }

    public IEnumerable<TWord> GetMatchingWords(string productName)
    {
        var result = new List<TWord>();

        result.AddRange(ExactSymbolsMatchItems
            .Where(sw => !string.IsNullOrEmpty(sw.Word) &&
                         productName.Contains(sw.Word, StringComparison.OrdinalIgnoreCase)));

        foreach (var pair in ExactWordRegexes)
        {
            if (pair.regex.IsMatch(productName))
                result.Add(pair.word);
        }

        foreach (var pair in PhraseRegexes)
        {
            if (pair.regex.IsMatch(productName))
                result.Add(pair.word);
        }

        return result;
    }
}

