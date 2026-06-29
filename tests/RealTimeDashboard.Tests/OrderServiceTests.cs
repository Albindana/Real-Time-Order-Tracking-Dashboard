using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Application.Services;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Domain.Enums;
using RealTimeDashboard.Domain.Exceptions;
using RealTimeDashboard.Infrastructure.Persistence;

namespace RealTimeDashboard.Tests;

public class OrderServiceTests
{
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly Mock<IDashboardStatsService> _stats = new();

    private OrderService CreateService(AppDbContext db) =>
        new(db, _publisher.Object, _stats.Object, NullLogger<OrderService>.Instance);

    private static Product SeedProduct(AppDbContext db, int stock = 10, decimal price = 25m)
    {
        var product = new Product
        {
            Name = "Widget",
            Category = "Test",
            Price = price,
            StockQuantity = stock,
            IsActive = true
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    [Fact]
    public async Task PlaceOrder_SavesOrder_AndPublishesEvent()
    {
        using var db = TestDbContextFactory.Create();
        var product = SeedProduct(db, stock: 10, price: 20m);
        var service = CreateService(db);

        var request = new CreateOrderRequest(new() { new CreateOrderItemRequest(product.Id, 2) });
        var result = await service.PlaceOrderAsync(request, "customer-1");

        Assert.Equal(40m, result.TotalAmount);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Single(db.Orders);

        // stock decremented
        Assert.Equal(8, db.Products.Single().StockQuantity);

        _publisher.Verify(p => p.PublishOrderPlacedAsync(
            It.Is<OrderSummaryDto>(o => o.TotalAmount == 40m), It.IsAny<CancellationToken>()), Times.Once);
        _stats.Verify(s => s.RefreshStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_PublishesLowStockAlert_WhenStockDropsBelowThreshold()
    {
        using var db = TestDbContextFactory.Create();
        var product = SeedProduct(db, stock: 6); // ordering 2 leaves 4 (< 5)
        var service = CreateService(db);

        var request = new CreateOrderRequest(new() { new CreateOrderItemRequest(product.Id, 2) });
        await service.PlaceOrderAsync(request, "customer-1");

        _publisher.Verify(p => p.PublishLowStockAsync(
            It.Is<LowStockAlertDto>(a => a.CurrentStock == 4), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrder_Throws_WhenProductOutOfStock()
    {
        using var db = TestDbContextFactory.Create();
        var product = SeedProduct(db, stock: 1);
        var service = CreateService(db);

        var request = new CreateOrderRequest(new() { new CreateOrderItemRequest(product.Id, 5) });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PlaceOrderAsync(request, "customer-1"));

        Assert.Empty(db.Orders);
        _publisher.Verify(p => p.PublishOrderPlacedAsync(It.IsAny<OrderSummaryDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_PublishesStatusChangedEvent_WithOldAndNewStatus()
    {
        using var db = TestDbContextFactory.Create();
        var product = SeedProduct(db);
        var service = CreateService(db);

        var placed = await service.PlaceOrderAsync(
            new CreateOrderRequest(new() { new CreateOrderItemRequest(product.Id, 1) }), "customer-1");

        var update = await service.UpdateStatusAsync(placed.Id, OrderStatus.Shipped);

        Assert.Equal(OrderStatus.Pending, update.OldStatus);
        Assert.Equal(OrderStatus.Shipped, update.NewStatus);
        Assert.Equal(OrderStatus.Shipped, db.Orders.Single().Status);

        _publisher.Verify(p => p.PublishOrderStatusChangedAsync(
            It.Is<OrderStatusUpdateDto>(u => u.NewStatus == OrderStatus.Shipped),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_Throws_WhenOrderNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateStatusAsync(Guid.NewGuid(), OrderStatus.Delivered));
    }
}
