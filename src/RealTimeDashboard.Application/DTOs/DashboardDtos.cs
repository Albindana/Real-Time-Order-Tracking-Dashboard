namespace RealTimeDashboard.Application.DTOs;

public record DashboardStatsDto(
    int TotalOrdersToday,
    decimal RevenueToday,
    int PendingOrders,
    int ActiveConnections,
    List<OrderSummaryDto> RecentOrders
);

public record LowStockAlertDto(
    Guid ProductId,
    string ProductName,
    int CurrentStock
);
