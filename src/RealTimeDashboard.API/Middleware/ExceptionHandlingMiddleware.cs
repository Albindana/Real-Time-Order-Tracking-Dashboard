using System.Text.Json;
using RealTimeDashboard.Application.Common;
using RealTimeDashboard.Domain.Exceptions;

namespace RealTimeDashboard.API.Middleware;

/// <summary>Translates domain exceptions into RFC-7807-style JSON problem responses.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(context, ex);
        }
    }

    private async Task WriteResponseAsync(HttpContext context, Exception ex)
    {
        var (status, title, errors) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message, (object?)null),
            ValidationException ve => (StatusCodes.Status400BadRequest, ve.Message, ve.Errors),
            BusinessRuleException => (StatusCodes.Status409Conflict, ex.Message, null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status,
            title,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonDefaults.Options));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
