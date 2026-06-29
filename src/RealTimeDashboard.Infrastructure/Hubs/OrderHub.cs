using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealTimeDashboard.Application.Events;
using RealTimeDashboard.Application.Interfaces;

namespace RealTimeDashboard.Infrastructure.Hubs;

/// <summary>
/// Server-push only hub. Clients connect and listen; they never invoke server methods.
/// Lives in Infrastructure so the OrderEventWorker (also Infrastructure) can reference its
/// typed <see cref="IHubContext{OrderHub}"/> without the API project creating a dependency cycle.
/// </summary>
[Authorize]
public class OrderHub : Hub
{
    private readonly IDashboardStatsService _statsService;

    public OrderHub(IDashboardStatsService statsService)
    {
        _statsService = statsService;
    }

    public override async Task OnConnectedAsync()
    {
        await _statsService.IncrementActiveConnectionsAsync();

        var stats = await _statsService.GetCurrentStatsAsync();
        await Clients.Caller.SendAsync(HubEvents.InitialStats, stats);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _statsService.DecrementActiveConnectionsAsync();
        await base.OnDisconnectedAsync(exception);
    }
}
