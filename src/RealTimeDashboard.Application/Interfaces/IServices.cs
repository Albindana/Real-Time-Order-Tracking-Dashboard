using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Domain.Enums;

namespace RealTimeDashboard.Application.Interfaces;

public interface IOrderService
{
    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(PaginationQuery query, CancellationToken ct = default);
    Task<PagedResult<OrderSummaryDto>> GetOrdersForCustomerAsync(string customerId, PaginationQuery query, CancellationToken ct = default);
    Task<OrderDetailDto> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDetailDto> PlaceOrderAsync(CreateOrderRequest request, string customerId, CancellationToken ct = default);
    Task<OrderStatusUpdateDto> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(PaginationQuery query, CancellationToken ct = default);
    Task<ProductDto> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
}

public interface IDashboardStatsService
{
    Task<DashboardStatsDto> GetCurrentStatsAsync(CancellationToken ct = default);
    Task<DashboardStatsDto> RefreshStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryDto>> GetRecentOrdersAsync(int count = 20, CancellationToken ct = default);
    Task<int> IncrementActiveConnectionsAsync();
    Task<int> DecrementActiveConnectionsAsync();
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
}

/// <summary>Publishes order events to Redis pub/sub. Implemented in Infrastructure.</summary>
public interface IEventPublisher
{
    Task PublishOrderPlacedAsync(OrderSummaryDto order, CancellationToken ct = default);
    Task PublishOrderStatusChangedAsync(OrderStatusUpdateDto update, CancellationToken ct = default);
    Task PublishLowStockAsync(LowStockAlertDto alert, CancellationToken ct = default);
}
