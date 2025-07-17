namespace Logibooks.Core.Services;

using System.Collections.Generic;
using Logibooks.Core.Models;

public interface IMorphologySearchService
{
    MorphologyContext InitializeContext(IEnumerable<StopWord> stopWords);
    IEnumerable<int> CheckText(MorphologyContext context, string text);
}

public class MorphologyContext
{
    internal Dictionary<Pullenti.Semantic.Utils.DerivateGroup, HashSet<int>> Groups { get; } = new();
}
