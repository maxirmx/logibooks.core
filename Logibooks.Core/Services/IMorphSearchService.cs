namespace Logibooks.Core.Services;

public record SearchKeyword(int Id, string Word);

public interface IMorphSearchService
{
    /// <summary>
    /// Initializes search context and returns lemmas for provided keywords.
    /// </summary>
    Task<IReadOnlyList<string>> InitializeAsync(IEnumerable<SearchKeyword> keywords, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks text for presence of keyword lemmas and returns their ids.
    /// </summary>
    Task<IReadOnlyCollection<int>> CheckTextAsync(string text, CancellationToken cancellationToken = default);
}
