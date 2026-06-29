using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Domain.Enums;

namespace RealTimeDashboard.Tests;

public class OrdersApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    // Match the API's contract: enums as strings, camelCase property names.
    private static readonly System.Text.Json.JsonSerializerOptions Json = JsonDefaults.Options;

    public OrdersApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@dashboard.com", "Admin123!"));
        login.EnsureSuccessStatusCode();

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> FirstProductIdAsync(HttpClient client)
    {
        var products = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?pageSize=1", Json);
        return products!.Items[0].Id;
    }

    [Fact]
    public async Task PostOrder_Returns201_AndOrderAppearsInRecent()
    {
        var client = await CreateAuthedClientAsync();
        var productId = await FirstProductIdAsync(client);

        var response = await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(new() { new CreateOrderItemRequest(productId, 1) }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Json);
        Assert.NotNull(created);

        var recent = await client.GetFromJsonAsync<List<OrderSummaryDto>>("/api/dashboard/recent", Json);
        Assert.Contains(recent!, o => o.Id == created!.Id);
    }

    [Fact]
    public async Task PutStatus_UpdatesOrderStatus()
    {
        var client = await CreateAuthedClientAsync();
        var productId = await FirstProductIdAsync(client);

        var createResp = await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(new() { new CreateOrderItemRequest(productId, 1) }));
        var created = await createResp.Content.ReadFromJsonAsync<OrderDetailDto>(Json);

        var statusResp = await client.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateOrderStatusRequest(OrderStatus.Shipped));
        statusResp.EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{created.Id}", Json);
        Assert.Equal(OrderStatus.Shipped, detail!.Status);
    }

    [Fact]
    public async Task GetStats_ReflectsTodaysOrders()
    {
        var client = await CreateAuthedClientAsync();
        var productId = await FirstProductIdAsync(client);

        var before = await client.GetFromJsonAsync<DashboardStatsDto>("/api/dashboard/stats", Json);

        await client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(new() { new CreateOrderItemRequest(productId, 1) }));

        var after = await client.GetFromJsonAsync<DashboardStatsDto>("/api/dashboard/stats", Json);

        Assert.True(after!.TotalOrdersToday >= before!.TotalOrdersToday + 1);
    }

    [Fact]
    public async Task GetOrders_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
