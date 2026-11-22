using FieldDataCapturePlatform.Hubs;
using FieldDataCapturePlatform.Models;
using Microsoft.AspNetCore.SignalR;

namespace FieldDataCapturePlatform.Services;

public sealed class EventsHubContext(IHubContext<EventsHub> hubContext)
{
    private readonly IHubContext<EventsHub> _hubContext = hubContext;

    public Task PublishAsync(CapturedEvent capturedEvent) =>
        _hubContext.Clients.All.SendAsync("eventCaptured", capturedEvent);

    public Task PublishRestoreAsync() =>
        _hubContext.Clients.All.SendAsync("dataChanged");
}
