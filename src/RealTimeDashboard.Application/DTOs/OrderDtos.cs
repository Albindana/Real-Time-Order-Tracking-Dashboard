using RealTimeDashboard.Domain.Enums;

namespace RealTimeDashboard.Application.DTOs;

public record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt
);

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderItemDto> Items
);

public record OrderStatusUpdateDto(
    Guid OrderId,
    string OrderNumber,
    OrderStatus OldStatus,
    OrderStatus NewStatus,
    DateTime UpdatedAt
);

public record CreateOrderItemRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(List<CreateOrderItemRequest> Items);

public record UpdateOrderStatusRequest(OrderStatus Status);
