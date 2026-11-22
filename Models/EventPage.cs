namespace FieldDataCapturePlatform.Models;

public sealed class EventPage
{
    public IReadOnlyList<CapturedEvent> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
