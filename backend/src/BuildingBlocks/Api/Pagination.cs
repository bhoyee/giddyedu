namespace GiddyEdu.BuildingBlocks.Api;

public sealed record PageRequest(int Page = 1, int PageSize = 25)
{
    public int ValidatedPage => Page < 1 ? 1 : Page;
    public int ValidatedPageSize => Math.Clamp(PageSize, 1, 100);
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
