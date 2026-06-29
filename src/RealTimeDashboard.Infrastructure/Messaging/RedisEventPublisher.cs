using System.Text.Json;
using StackExchange.Redis;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Events;
using RealTimeDashboard.Application.Interfaces;

namespace RealTimeDashboard.Infrastructure.Messaging;

/// <summary>
/// Publishes order events onto the Redis pub/sub channel. The controller path ends here;
/// broadcasting to SignalR clients is the BackgroundService's job. This decouples the
/// broadcast from the HTTP request lifecycle and lets multiple API instances scale out.
/// </summary>
public class RedisEventPublisher : IEventPublisher
{
    private readonly ISubscriber _subscriber;

    public RedisEventPublisher(IConnectionMultiplexer redis)
    {
        _subscriber = redis.GetSubscriber();
    }

    public Task PublishOrderPlacedAsync(OrderSummaryDto order, CancellationToken ct = default) =>
        PublishAsync(OrderEventTypes.OrderPlaced, order.Id, order);

    public Task PublishOrderStatusChangedAsync(OrderStatusUpdateDto update, CancellationToken ct = default) =>
        PublishAsync(OrderEventTypes.OrderStatusChanged, update.OrderId, update);

    public Task PublishLowStockAsync(LowStockAlertDto alert, CancellationToken ct = default) =>
        PublishAsync(OrderEventTypes.LowStock, alert.ProductId, alert);

    private async Task PublishAsync<T>(string eventType, Guid id, T payload)
    {
        var evt = new OrderEvent
        {
            EventType = eventType,
            OrderId = id,
            Payload = JsonSerializer.Serialize(payload, JsonDefaults.Options)
        };

        await _subscriber.PublishAsync(
            RedisChannel.Literal(RedisKeys.OrderEventsChannel),
            JsonSerializer.Serialize(evt, JsonDefaults.Options));
    }
}
