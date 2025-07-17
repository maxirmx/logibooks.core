namespace Logibooks.Core.Services;

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pullenti.Morph;
using Pullenti.Semantic.Utils;
using Logibooks.Core.Models;

public class MorphologySearchService : IMorphologySearchService
{
    private static readonly Regex WordRegex = new Regex(@"\p{L}+", RegexOptions.Compiled);
    static MorphologySearchService()
    {
        MorphologyService.Initialize(Pullenti.Morph.MorphLang.RU);
        DerivateService.Initialize(Pullenti.Morph.MorphLang.RU);
    }

    public MorphologyContext InitializeContext(IEnumerable<StopWord> stopWords)
    {
        var context = new MorphologyContext();
        foreach (var sw in stopWords)
        {
            if (string.IsNullOrWhiteSpace(sw.Word))
                continue;
            var groups = DerivateService.FindDerivates(sw.Word.ToUpperInvariant(), true, null);
            if (groups == null)
                continue;
            foreach (var g in groups)
            {
                if (!context.Groups.TryGetValue(g, out var ids))
                {
                    ids = new HashSet<int>();
                    context.Groups[g] = ids;
                }
                ids.Add(sw.Id);
            }
        }
        return context;
    }

    public IEnumerable<int> CheckText(MorphologyContext context, string text)
    {
        var result = new HashSet<int>();
        foreach (Match m in WordRegex.Matches(text ?? string.Empty))
        {
            var tokenGroups = DerivateService.FindDerivates(m.Value.ToUpperInvariant(), true, null);
            if (tokenGroups == null)
                continue;
            foreach (var g in tokenGroups)
            {
                if (context.Groups.TryGetValue(g, out var ids))
                    result.UnionWith(ids);
            }
        }
        return result;
    }
}
