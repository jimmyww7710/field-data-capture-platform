using System.Text.Json;
using FieldDataCapturePlatform.Models;

namespace FieldDataCapturePlatform.Services;

public sealed class DataStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _dataDirectory;
    private readonly string _eventsFile;
    private readonly string _backupDirectory;
    private readonly string _backupIndexFile;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public DataStore(IHostEnvironment environment)
    {
        var contentRoot = environment.ContentRootPath;
        _dataDirectory = Path.Combine(contentRoot, "Data");
        _backupDirectory = Path.Combine(contentRoot, "Backups");
        _eventsFile = Path.Combine(_dataDirectory, "events.json");
        _backupIndexFile = Path.Combine(_backupDirectory, "backups.json");

        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task<CapturedEvent> IngestAsync(IngestEventRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var events = await ReadEventsAsync();
            var capturedEvent = new CapturedEvent
            {
                DeviceId = request.DeviceId.Trim(),
                EventType = request.EventType.Trim(),
                Metric = request.Metric.Trim(),
                Value = request.Value,
                Unit = request.Unit.Trim()
            };

            events.Insert(0, capturedEvent);
            await WriteEventsAsync(events);
            return capturedEvent;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EventPage> QueryAsync(
        string? deviceId,
        string? status,
        string? search,
        int page,
        int pageSize)
    {
        await _gate.WaitAsync();
        try
        {
            var events = await ReadEventsAsync();
            IEnumerable<CapturedEvent> filtered = events;

            if (!string.IsNullOrWhiteSpace(deviceId))
                filtered = filtered.Where(item =>
                    item.DeviceId.Contains(deviceId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(status))
                filtered = filtered.Where(item =>
                    item.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(item =>
                    item.EventType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Metric.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.DeviceId.Contains(search, StringComparison.OrdinalIgnoreCase));

            var ordered = filtered
                .OrderByDescending(item => item.CapturedAt)
                .ToList();

            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new EventPage
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = ordered.Count
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CapturedEvent?> ReviewAsync(
        Guid id,
        string status,
        string? reviewer,
        string? note)
    {
        await _gate.WaitAsync();
        try
        {
            var events = await ReadEventsAsync();
            var existing = events.FirstOrDefault(item => item.Id == id);
            if (existing is null)
                return null;

            existing.Status = status;
            existing.Reviewer = reviewer;
            existing.ReviewNote = note;
            await WriteEventsAsync(events);
            return existing;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupRecord> CreateBackupAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var events = await ReadEventsAsync();
            var record = new BackupRecord
            {
                Id = Guid.NewGuid(),
                EventCount = events.Count
            };

            var backupFile = GetBackupFile(record.Id);
            await File.WriteAllTextAsync(
                backupFile,
                JsonSerializer.Serialize(events, _jsonOptions));

            record = new BackupRecord
            {
                Id = record.Id,
                EventCount = record.EventCount,
                SizeBytes = new FileInfo(backupFile).Length,
                CreatedAt = record.CreatedAt
            };

            var records = await ReadBackupRecordsAsync();
            records.Insert(0, record);
            await WriteBackupRecordsAsync(records);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadBackupRecordsAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RestoreBackupAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            var records = await ReadBackupRecordsAsync();
            if (records.All(record => record.Id != id))
                return false;

            var backupFile = GetBackupFile(id);
            if (!File.Exists(backupFile))
                return false;

            var json = await File.ReadAllTextAsync(backupFile);
            var events = JsonSerializer.Deserialize<List<CapturedEvent>>(json, _jsonOptions) ?? [];
            await WriteEventsAsync(events);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await WriteEventsAsync([]);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static IngestEventRequest CreateSimulatedRequest(string? deviceId)
    {
        var random = Random.Shared;
        var devices = new[] { "sensor-north", "sensor-west", "sensor-lab", "sensor-qa" };
        var metrics = new[]
        {
            ("temperature", "C"),
            ("humidity", "%"),
            ("pressure", "kPa"),
            ("vibration", "mm/s"),
            ("battery", "%")
        };
        var eventTypes = new[] { "measurement", "heartbeat", "alert", "calibration" };
        var selectedMetric = metrics[random.Next(metrics.Length)];

        return new IngestEventRequest(
            string.IsNullOrWhiteSpace(deviceId)
                ? devices[random.Next(devices.Length)]
                : deviceId.Trim(),
            eventTypes[random.Next(eventTypes.Length)],
            selectedMetric.Item1,
            Math.Round(random.NextDouble() * 100, 2),
            selectedMetric.Item2);
    }

    private async Task<List<CapturedEvent>> ReadEventsAsync()
    {
        if (!File.Exists(_eventsFile))
            return [];

        var json = await File.ReadAllTextAsync(_eventsFile);
        return JsonSerializer.Deserialize<List<CapturedEvent>>(json, _jsonOptions) ?? [];
    }

    private async Task WriteEventsAsync(List<CapturedEvent> events)
    {
        await File.WriteAllTextAsync(
            _eventsFile,
            JsonSerializer.Serialize(events, _jsonOptions));
    }

    private async Task<List<BackupRecord>> ReadBackupRecordsAsync()
    {
        if (!File.Exists(_backupIndexFile))
            return [];

        var json = await File.ReadAllTextAsync(_backupIndexFile);
        return JsonSerializer.Deserialize<List<BackupRecord>>(json, _jsonOptions) ?? [];
    }

    private async Task WriteBackupRecordsAsync(List<BackupRecord> records)
    {
        await File.WriteAllTextAsync(
            _backupIndexFile,
            JsonSerializer.Serialize(records, _jsonOptions));
    }

    private string GetBackupFile(Guid id) =>
        Path.Combine(_backupDirectory, $"{id}.json");
}
