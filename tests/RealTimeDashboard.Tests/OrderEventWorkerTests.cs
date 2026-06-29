using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Events;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Domain.Enums;
using RealTimeDashboard.Infrastructure.BackgroundServices;
using RealTimeDashboard.Infrastructure.Hubs;

namespace RealTimeDashboard.Tests;

public class OrderEventWorkerTests
{
    private readonly Mock<IClientProxy> _allClients = new();
    private readonly Mock<IHubClients> _hubClients = new();
    private readonly Mock<IHubContext<OrderHub>> _hubContext = new();
    private readonly Mock<IDashboardStatsService> _stats = new();

    private OrderEventWorker CreateWorker()
    {
        _hubClients.Setup(c => c.All).Returns(_allClients.Object);
        _hubContext.Setup(c => c.Clients).Returns(_hubClients.Object);

        _stats.Setup(s => s.GetCurrentStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardStatsDto(0, 0, 0, 1, new()));

        // Minimal scope plumbing so the worker can resolve a scoped IDashboardStatsService.
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IDashboardStatsService))).Returns(_stats.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var redis = new Mock<IConnectionMultiplexer>();

        return new OrderEventWorker(
            redis.Object, _hubContext.Object, scopeFactory.Object, NullLogger<OrderEventWorker>.Instance);
    }

    private static string Envelope(string eventType, object payload) =>
        JsonSerializer.Serialize(new OrderEvent
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, JsonDefaults.Options)
        }, JsonDefaults.Options);

    [Fact]
    public async Task NewOrderPlaced_BroadcastsOrderAndStats()
    {
        var worker = CreateWorker();
        var order = new OrderSummaryDto(Guid.NewGuid(), "ORD-1", "Jane", "j@x.com",
            OrderStatus.Pending, 99m, 1, DateTime.UtcNow);

        await worker.HandleMessageAsync(Envelope(OrderEventTypes.OrderPlaced, order), CancellationToken.None);

        _allClients.Verify(c => c.SendCoreAsync(HubEvents.NewOrderPlaced,
            It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _allClients.Verify(c => c.SendCoreAsync(HubEvents.StatsUpdated,
            It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrderStatusChanged_BroadcastsStatusUpdate()
    {
        var worker = CreateWorker();
        var update = new OrderStatusUpdateDto(Guid.NewGuid(), "ORD-1",
            OrderStatus.Pending, OrderStatus.Shipped, DateTime.UtcNow);

        await worker.HandleMessageAsync(Envelope(OrderEventTypes.OrderStatusChanged, update), CancellationToken.None);

        _allClients.Verify(c => c.SendCoreAsync(HubEvents.OrderStatusChanged,
            It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MalformedMessage_IsSwallowed_AndDoesNotBroadcast()
    {
        var worker = CreateWorker();

        // Should not throw.
        await worker.HandleMessageAsync("this is not json", CancellationToken.None);

        _allClients.Verify(c => c.SendCoreAsync(It.IsAny<string>(),
            It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
