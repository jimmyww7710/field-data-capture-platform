using FieldDataCapturePlatform.Hubs;
using FieldDataCapturePlatform.Models;
using FieldDataCapturePlatform.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<EventsHubContext>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<EventsHub>("/hubs/events");

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Field Data Capture Platform",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapPost("/api/events", async (IngestEventRequest request, DataStore store, EventsHubContext hub) =>
{
    if (string.IsNullOrWhiteSpace(request.DeviceId))
        return Results.BadRequest(new { error = "DeviceId is required." });
    if (string.IsNullOrWhiteSpace(request.EventType))
        return Results.BadRequest(new { error = "EventType is required." });

    var created = await store.IngestAsync(request);
    await hub.PublishAsync(created);
    return Results.Created($"/api/events/{created.Id}", created);
});

app.MapGet("/api/events", async (
    DataStore store,
    string? deviceId,
    string? status,
    string? search,
    int page = 1,
    int pageSize = 25) =>
{
    if (page < 1) page = 1;
    if (pageSize is < 1 or > 200) pageSize = 25;
    return Results.Ok(await store.QueryAsync(deviceId, status, search, page, pageSize));
});

app.MapPost("/api/events/{id:guid}/review", async (
    Guid id,
    ReviewRequest request,
    DataStore store,
    EventsHubContext hub) =>
{
    var updated = await store.ReviewAsync(id, request.Status, request.Reviewer, request.Note);
    if (updated is null)
        return Results.NotFound(new { error = "Event not found." });

    await hub.PublishAsync(updated);
    return Results.Ok(updated);
});

app.MapPost("/api/simulator", async (SimulatorRequest request, DataStore store, EventsHubContext hub) =>
{
    var count = Math.Clamp(request.Count, 1, 100);
    var createdEvents = new List<CapturedEvent>(count);

    for (var i = 0; i < count; i++)
        createdEvents.Add(await store.IngestAsync(DataStore.CreateSimulatedRequest(request.DeviceId)));

    foreach (var capturedEvent in createdEvents)
        await hub.PublishAsync(capturedEvent);

    return Results.Ok(new { count = createdEvents.Count, events = createdEvents });
});

app.MapPost("/api/backups", async (DataStore store) => Results.Ok(await store.CreateBackupAsync()));

app.MapGet("/api/backups", async (DataStore store) => Results.Ok(await store.ListBackupsAsync()));

app.MapPost("/api/backups/{id:guid}/restore", async (Guid id, DataStore store, EventsHubContext hub) =>
{
    var restored = await store.RestoreBackupAsync(id);
    if (!restored)
        return Results.NotFound(new { error = "Backup not found." });

    await hub.PublishRestoreAsync();
    return Results.Ok(new { restored = true, backupId = id });
});

app.MapPost("/api/demo/reset", async (DataStore store, EventsHubContext hub) =>
{
    await store.ResetAsync();
    await hub.PublishRestoreAsync();
    return Results.Ok(new { reset = true });
});

app.MapFallbackToFile("/index.html");

app.Run();
