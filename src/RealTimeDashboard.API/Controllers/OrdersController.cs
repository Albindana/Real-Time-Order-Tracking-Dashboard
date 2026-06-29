using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Infrastructure.Auth;

namespace RealTimeDashboard.API.Controllers;

[Authorize]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [Authorize(Roles = AuthService.AdminRole)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetAll(
        [FromQuery] PaginationQuery query, CancellationToken ct)
        => Ok(await _orderService.GetOrdersAsync(query, ct));

    [HttpGet("my")]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetMine(
        [FromQuery] PaginationQuery query, CancellationToken ct)
        => Ok(await _orderService.GetOrdersForCustomerAsync(CurrentUserId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _orderService.GetOrderByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> Place(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.PlaceOrderAsync(request, CurrentUserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = AuthService.AdminRole)]
    public async Task<ActionResult<OrderStatusUpdateDto>> UpdateStatus(
        Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
        => Ok(await _orderService.UpdateStatusAsync(id, request.Status, ct));
}
