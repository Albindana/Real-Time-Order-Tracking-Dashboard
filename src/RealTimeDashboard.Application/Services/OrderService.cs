using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Application.Mapping;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Domain.Enums;
using RealTimeDashboard.Domain.Exceptions;

namespace RealTimeDashboard.Application.Services;

public class OrderService : IOrderService
{
    private const int LowStockThreshold = 5;

    private readonly IApplicationDbContext _db;
    private readonly IEventPublisher _publisher;
    private readonly IDashboardStatsService _statsService;
    private readonly ILogger<OrderService> _logger;
    private readonly OrderMapper _mapper = new();

    public OrderService(
        IApplicationDbContext db,
        IEventPublisher publisher,
        IDashboardStatsService statsService,
        ILogger<OrderService> logger)
    {
        _db = db;
        _publisher = publisher;
        _statsService = statsService;
        _logger = logger;
    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt);
        var total = await baseQuery.CountAsync(ct);
        var entities = await baseQuery
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(ct);
        var items = entities.Select(_mapper.ToSummary).ToList();

        return new PagedResult<OrderSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersForCustomerAsync(string customerId, PaginationQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt);

        var total = await baseQuery.CountAsync(ct);
        var entities = await baseQuery
            .Skip(query.Skip).Take(query.PageSize)
            .ToListAsync(ct);
        var items = entities.Select(_mapper.ToSummary).ToList();

        return new PagedResult<OrderSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<OrderDetailDto> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(Order), id);

        return _mapper.ToDetail(order);
    }

    public async Task<OrderDetailDto> PlaceOrderAsync(CreateOrderRequest request, string customerId, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new BusinessRuleException("An order must contain at least one item.");

        // Collapse duplicate product lines so stock math and snapshots stay correct.
        var requested = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var productIds = requested.Keys.ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct);

        var order = new Order
        {
            CustomerId = customerId,
            OrderNumber = await GenerateOrderNumberAsync(ct),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var lowStockProducts = new List<Product>();

        foreach (var (productId, quantity) in requested)
        {
            var product = products.FirstOrDefault(p => p.Id == productId)
                ?? throw new NotFoundException(nameof(Product), productId);

            if (!product.IsActive)
                throw new BusinessRuleException($"Product '{product.Name}' is not available.");

            if (product.StockQuantity < quantity)
                throw new BusinessRuleException(
                    $"Insufficient stock for '{product.Name}'. Requested {quantity}, available {product.StockQuantity}.");

            product.StockQuantity -= quantity;

            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price
            });

            if (product.StockQuantity < LowStockThreshold)
                lowStockProducts.Add(product);
        }

        order.ItemCount = order.Items.Sum(i => i.Quantity);
        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        // Snapshot customer identity from the most recent order, or fall back to the id.
        var existingProfile = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.CustomerName, o.CustomerEmail })
            .FirstOrDefaultAsync(ct);

        order.CustomerName = existingProfile?.CustomerName ?? customerId;
        order.CustomerEmail = existingProfile?.CustomerEmail ?? string.Empty;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderNumber} placed for customer {CustomerId}", order.OrderNumber, customerId);

        var summary = _mapper.ToSummary(order);

        // Refresh the cached stats first so listeners receive fresh numbers, then publish.
        await _statsService.RefreshStatsAsync(ct);
        await _publisher.PublishOrderPlacedAsync(summary, ct);

        foreach (var product in lowStockProducts)
        {
            await _publisher.PublishLowStockAsync(
                new LowStockAlertDto(product.Id, product.Name, product.StockQuantity), ct);
        }

        return _mapper.ToDetail(order);
    }

    public async Task<OrderStatusUpdateDto> UpdateStatusAsync(Guid id, OrderStatus newStatus, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(Order), id);

        var oldStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderNumber} status changed {Old} -> {New}", order.OrderNumber, oldStatus, newStatus);

        var update = new OrderStatusUpdateDto(order.Id, order.OrderNumber, oldStatus, newStatus, order.UpdatedAt);

        await _statsService.RefreshStatsAsync(ct);
        await _publisher.PublishOrderStatusChangedAsync(update, ct);

        return update;
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Orders.CountAsync(ct);
        return $"ORD-{year}-{count + 1:D5}";
    }
}
