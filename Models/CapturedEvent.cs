namespace FieldDataCapturePlatform.Models;

public sealed class CapturedEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DeviceId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Reviewer { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record IngestEventRequest(
    string DeviceId,
    string EventType,
    string Metric,
    double Value,
    string Unit);

public sealed record ReviewRequest(string Status, string? Reviewer, string? Note);

public sealed record SimulatorRequest(string? DeviceId, int Count = 10);

