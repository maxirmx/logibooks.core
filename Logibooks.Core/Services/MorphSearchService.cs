using System.Text.RegularExpressions;
using DeepMorphy;

namespace Logibooks.Core.Services;

public sealed class MorphSearchService : IMorphSearchService
{
    private readonly MorphAnalyzer _morph = new(withLemmatization: true);
    private readonly Regex _tokenRegex = new("\\p{L}+", RegexOptions.Compiled);
    private readonly Dictionary<string, HashSet<int>> _lemmaToIds = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<string>> InitializeAsync(IEnumerable<SearchKeyword> keywords, CancellationToken cancellationToken = default)
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

    public Task<IReadOnlyCollection<int>> CheckTextAsync(string text, CancellationToken cancellationToken = default)
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
