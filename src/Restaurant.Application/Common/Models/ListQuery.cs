namespace Restaurant.Application.Common.Models;

public sealed class ListQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? SortBy { get; set; }

    public string? SortDir { get; set; } = "asc";

    public string? Search { get; set; }

    public bool IncludeInactive { get; set; }

    public Dictionary<string, string>? Filters { get; set; }

    public (int Page, int PageSize) Normalize(int maxPageSize = 100)
    {
        var page = Page < 1 ? 1 : Page;
        var allowed = new[] { 10, 25, 50, 100 };
        var pageSize = allowed.Contains(PageSize)
            ? PageSize
            : allowed.OrderBy(s => Math.Abs(s - PageSize)).First();
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);
        return (page, pageSize);
    }

    public bool IsDescending =>
        string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);

    public string? FilterValue(string key) =>
        Filters is not null && Filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
