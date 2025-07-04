namespace Logibooks.Core.RestModels;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
    public PaginationInfo Pagination { get; set; } = new();
    public SortingInfo Sorting { get; set; } = new();
    public string? Search { get; set; }
}
