using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Events;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Infrastructure.Hubs;

namespace RealTimeDashboard.Infrastructure.BackgroundServices;

/// <summary>
/// Subscribes to the Redis "order-events" channel and fans each event out to connected
/// SignalR clients. Running as an IHostedService keeps the long-lived subscription off the
/// HTTP request path, and routing through Redis means any API instance can broadcast.
/// </summary>
public class OrderEventWorker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderEventWorker> _logger;

    public OrderEventWorker(
        IConnectionMultiplexer redis,
        IHubContext<OrderHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderEventWorker> logger)
    {
        _redis = redis;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(RedisKeys.OrderEventsChannel),
            (channel, message) => _ = HandleMessageAsync(message!, stoppingToken));

        _logger.LogInformation("OrderEventWorker subscribed to '{Channel}'", RedisKeys.OrderEventsChannel);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            await subscriber.UnsubscribeAllAsync();
        }
    }

    private async Task HandleMessageAsync(string message, CancellationToken ct)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<OrderEvent>(message, JsonDefaults.Options);
            if (evt is null)
            {
                _logger.LogWarning("Received null/unparsable order event payload.");
                return;
            }

            switch (evt.EventType)
            {
                case OrderEventTypes.OrderPlaced:
                {
                    var order = JsonSerializer.Deserialize<OrderSummaryDto>(evt.Payload, JsonDefaults.Options);
                    await _hubContext.Clients.All.SendAsync(HubEvents.NewOrderPlaced, order, ct);
                    await BroadcastStatsAsync(ct);
                    break;
                }
                case OrderEventTypes.OrderStatusChanged:
                {
                    var update = JsonSerializer.Deserialize<OrderStatusUpdateDto>(evt.Payload, JsonDefaults.Options);
                    await _hubContext.Clients.All.SendAsync(HubEvents.OrderStatusChanged, update, ct);
                    await BroadcastStatsAsync(ct);
                    break;
                }
                case OrderEventTypes.LowStock:
                {
                    var alert = JsonSerializer.Deserialize<LowStockAlertDto>(evt.Payload, JsonDefaults.Options);
                    await _hubContext.Clients.All.SendAsync(HubEvents.LowStockAlert, alert, ct);
                    break;
                }
                default:
                    _logger.LogWarning("Unknown order event type '{EventType}'", evt.EventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Swallow per-message failures so a single bad payload never tears down the subscriber.
            _logger.LogError(ex, "Error processing order event");
        }
    }

    private async Task BroadcastStatsAsync(CancellationToken ct)
    {
        // Stats service is scoped (uses DbContext), so resolve a fresh scope per broadcast.
        using var scope = _scopeFactory.CreateScope();
        var statsService = scope.ServiceProvider.GetRequiredService<IDashboardStatsService>();
        var stats = await statsService.GetCurrentStatsAsync(ct);
        await _hubContext.Clients.All.SendAsync(HubEvents.StatsUpdated, stats, ct);
    }
}
