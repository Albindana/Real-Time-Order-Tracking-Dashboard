using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;

namespace RealTimeDashboard.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardStatsService _statsService;

    public DashboardController(IDashboardStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats(CancellationToken ct)
        => Ok(await _statsService.GetCurrentStatsAsync(ct));

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetRecent(CancellationToken ct)
        => Ok(await _statsService.GetRecentOrdersAsync(20, ct));
}
