// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.Services;

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Pullenti.Morph;
using Pullenti.Semantic.Utils;
using Logibooks.Core.Models;
using Logibooks.Core.Interfaces;

public class MorphologySearchService : IMorphologySearchService
{
    private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    
    static MorphologySearchService()
    {
        MorphologyService.Initialize(Pullenti.Morph.MorphLang.RU);
        DerivateService.Initialize(Pullenti.Morph.MorphLang.RU);
    }

    private static string GetNormalForm(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;
            
        var morphs = MorphologyService.GetAllWordforms(word.ToUpperInvariant(), MorphLang.RU);
        return morphs.Count > 0 ? morphs.First().NormalCase.ToUpperInvariant() : word.ToUpperInvariant();
    }

    public MorphologySupportLevel CheckWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return MorphologySupportLevel.NoSupport;
        
        var normalForm = GetNormalForm(word);
        var morphs = MorphologyService.GetAllWordforms(normalForm, MorphLang.RU);
        var groups = DerivateService.FindDerivates(normalForm, true, MorphLang.RU);

        bool hasMorphs = morphs != null && morphs.Count > 0;
        bool hasGroups = groups != null && groups.Any();

        if (!hasMorphs && !hasGroups)
            return MorphologySupportLevel.NoSupport;
        if (hasMorphs && !hasGroups)
            return MorphologySupportLevel.FormsSupport;
        if (hasMorphs && hasGroups)
            return MorphologySupportLevel.FullSupport;
        // If only groups, but not morphs (should not happen, but for completeness)
        return MorphologySupportLevel.NoSupport;
    }

    public MorphologyContext InitializeContext(IEnumerable<StopWord> stopWords)
    {
        var context = new MorphologyContext();
        foreach (var sw in stopWords)
        {
            if (string.IsNullOrWhiteSpace(sw.Word))
                continue;

            var normalForm = GetNormalForm(sw.Word);

            if (sw.MatchTypeId == (int)WordMatchTypeCode.WeakMorphology)
            {
                if (!context.NormalForms.TryGetValue(normalForm, out var ids))
                {
                    ids = new HashSet<int>();
                    context.NormalForms[normalForm] = ids;
                }
                ids.Add(sw.Id);
            }
            else if (sw.MatchTypeId == (int)WordMatchTypeCode.StrongMorphology)
            {
                var groups = DerivateService.FindDerivates(normalForm, true, Pullenti.Morph.MorphLang.RU);
                if (groups == null || !groups.Any())
                    continue;

                foreach (var g in groups)
                {
                    if (g == null)
                        continue;

                    if (!context.Groups.TryGetValue(g, out var ids))
                    {
                        ids = new HashSet<int>();
                        context.Groups[g] = ids;
                    }
                    ids.Add(sw.Id);
                }
            }
        }
        return context;
    }

    public IEnumerable<int> CheckText(MorphologyContext context, string text)
    {
        if ((context?.Groups == null || !context.Groups.Any()) &&
            (context?.NormalForms == null || !context.NormalForms.Any()))
            return [];

        var result = new HashSet<int>();
        var matches = WordRegex.Matches(text ?? string.Empty);

        foreach (Match m in matches)
        {
            if (string.IsNullOrWhiteSpace(m.Value))
                continue;

            var upperValue = m.Value.ToUpperInvariant();
            // Get the normal form first, then find derivatives
            var morphs = MorphologyService.GetAllWordforms(upperValue, Pullenti.Morph.MorphLang.RU);
            var normalForm = morphs.Count > 0 ? morphs.First().NormalCase.ToUpperInvariant() : upperValue;

            var distinctNormalForms = morphs.Select(f => f.NormalCase.ToUpperInvariant()).Distinct().ToList();
            if (!distinctNormalForms.Contains(normalForm))
            {
                distinctNormalForms.Add(normalForm);
            }

            foreach (var nf in distinctNormalForms)
            {
                if (context.NormalForms.TryGetValue(nf, out var ids))
                {
                    result.UnionWith(ids);
                }
            }

            var tokenGroups = DerivateService.FindDerivates(normalForm, true, Pullenti.Morph.MorphLang.RU);
            if (tokenGroups != null)
            {
                foreach (var g in tokenGroups)
                {
                    if (g == null)
                        continue;

                    if (context.Groups.TryGetValue(g, out var ids))
                    {
                        result.UnionWith(ids);
                    }
                }
            }
        }
        return result;
    }
}
