// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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

