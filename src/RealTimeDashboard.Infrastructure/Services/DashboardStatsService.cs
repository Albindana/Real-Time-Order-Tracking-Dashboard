using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Events;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Application.Mapping;
using RealTimeDashboard.Domain.Enums;

namespace RealTimeDashboard.Infrastructure.Services;

public class DashboardStatsService : IDashboardStatsService
{
    private readonly IApplicationDbContext _db;
    private readonly IDatabase _redis;
    private readonly ILogger<DashboardStatsService> _logger;
    private readonly OrderMapper _mapper = new();

    public DashboardStatsService(
        IApplicationDbContext db,
        IConnectionMultiplexer redis,
        ILogger<DashboardStatsService> logger)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<DashboardStatsDto> GetCurrentStatsAsync(CancellationToken ct = default)
    {
        var cached = await _redis.StringGetAsync(RedisKeys.DashboardStats);
        if (cached.HasValue)
        {
            try
            {
                var stats = JsonSerializer.Deserialize<DashboardStatsDto>(cached!, JsonDefaults.Options);
                if (stats is not null)
                {
                    // The active-connection counter is authoritative and changes faster than the snapshot.
                    return stats with { ActiveConnections = await GetActiveConnectionsAsync() };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached dashboard stats; recomputing.");
            }
        }

        return await RefreshStatsAsync(ct);
    }

    public async Task<DashboardStatsDto> RefreshStatsAsync(CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var todaysOrders = _db.Orders.AsNoTracking().Where(o => o.CreatedAt >= todayUtc);

        var totalOrdersToday = await todaysOrders.CountAsync(ct);

        // SQLite can't translate SUM over decimal, so pull today's non-cancelled amounts and sum locally.
        var todaysAmounts = await todaysOrders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Select(o => o.TotalAmount)
            .ToListAsync(ct);
        var revenueToday = todaysAmounts.Sum();
        var pendingOrders = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var recentEntities = await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        var recent = recentEntities.Select(_mapper.ToSummary).ToList();

        var stats = new DashboardStatsDto(
            totalOrdersToday,
            revenueToday,
            pendingOrders,
            await GetActiveConnectionsAsync(),
            recent);

        await _redis.StringSetAsync(
            RedisKeys.DashboardStats,
            JsonSerializer.Serialize(stats, JsonDefaults.Options));

        return stats;
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetRecentOrdersAsync(int count = 20, CancellationToken ct = default)
    {
        var entities = await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

        return entities.Select(_mapper.ToSummary).ToList();
    }

    public async Task<int> IncrementActiveConnectionsAsync()
    {
        var value = await _redis.StringIncrementAsync(RedisKeys.ActiveConnections);
        return (int)value;
    }

    public async Task<int> DecrementActiveConnectionsAsync()
    {
        var value = await _redis.StringDecrementAsync(RedisKeys.ActiveConnections);
        if (value < 0)
        {
            // Guard against drift if a decrement outpaces increments (e.g. after a restart).
            await _redis.StringSetAsync(RedisKeys.ActiveConnections, 0);
            value = 0;
        }
        return (int)value;
    }

    private async Task<int> GetActiveConnectionsAsync()
    {
        var value = await _redis.StringGetAsync(RedisKeys.ActiveConnections);
        return value.HasValue && int.TryParse(value, out var count) ? Math.Max(count, 0) : 0;
    }
}
