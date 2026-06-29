namespace RealTimeDashboard.Application.Common;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public record PaginationQuery(int Page = 1, int PageSize = 20)
{
    private const int MaxPageSize = 100;

    public int Page { get; init; } = Page < 1 ? 1 : Page;
    public int PageSize { get; init; } = PageSize is < 1 or > MaxPageSize ? 20 : PageSize;

    public int Skip => (Page - 1) * PageSize;
}
