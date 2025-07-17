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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System.Text.RegularExpressions;
using DeepMorphy;

namespace Logibooks.Core.Services;

public sealed class MorphSearchService : IMorphSearchService
{
    private readonly MorphAnalyzer _morph = new(withLemmatization: true);
    private readonly Regex _tokenRegex = new("\\p{L}+", RegexOptions.Compiled);
    private readonly Dictionary<string, HashSet<int>> _lemmaToIds = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> InitializeAsync(IEnumerable<SearchKeyword> keywords)
    {
        _lemmaToIds.Clear();
        var list = keywords.ToList();
        var lemmas = _morph.Parse(list.Select(k => k.Word)).ToList();
        var result = new List<string>(lemmas.Count);
        for (int i = 0; i < lemmas.Count; i++)
        {
            var lemma = lemmas[i].BestTag?.Lemma ?? lemmas[i].Text;
            result.Add(lemma);
            if (!_lemmaToIds.TryGetValue(lemma, out var set))
            {
                set = new HashSet<int>();
                _lemmaToIds[lemma] = set;
            }
            set.Add(list[i].Id);
        }
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    public Task<IReadOnlyCollection<int>> CheckTextAsync(string text)
    {
        var tokens = _tokenRegex.Matches(text).Select(m => m.Value);
        var lemmas = _morph.Parse(tokens);
        var result = new HashSet<int>();
        foreach (var info in lemmas)
        {
            var lemma = info.BestTag?.Lemma ?? info.Text;
            if (_lemmaToIds.TryGetValue(lemma, out var ids))
            {
                result.UnionWith(ids);
            }
        }
        return Task.FromResult<IReadOnlyCollection<int>>(result);
    }
}
