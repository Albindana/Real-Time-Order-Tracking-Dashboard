namespace RealTimeDashboard.Application.Events;

/// <summary>
/// The envelope published to the Redis "order-events" channel. The API publishes;
/// the OrderEventWorker background service subscribes and fans out over SignalR.
/// </summary>
public class OrderEvent
{
    public string EventType { get; set; } = string.Empty;
    public Guid OrderId { get; set; }

    /// <summary>JSON-serialized payload whose shape depends on <see cref="EventType"/>.</summary>
    public string Payload { get; set; } = string.Empty;
}

public static class OrderEventTypes
{
    public const string OrderPlaced = "OrderPlaced";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string LowStock = "LowStock";
}

/// <summary>Redis keys and channels used across the application.</summary>
public static class RedisKeys
{
    public const string OrderEventsChannel = "order-events";
    public const string DashboardStats = "dashboard:stats";
    public const string ActiveConnections = "dashboard:active-connections";
}

/// <summary>SignalR server-to-client event names clients subscribe to.</summary>
public static class HubEvents
{
    public const string InitialStats = "InitialStats";
    public const string NewOrderPlaced = "NewOrderPlaced";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string StatsUpdated = "StatsUpdated";
    public const string LowStockAlert = "LowStockAlert";
}
