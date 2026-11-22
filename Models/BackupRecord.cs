namespace FieldDataCapturePlatform.Models;

public sealed class BackupRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int EventCount { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
