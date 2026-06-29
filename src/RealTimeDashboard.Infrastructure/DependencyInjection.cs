using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Infrastructure.Auth;
using RealTimeDashboard.Infrastructure.Messaging;
using RealTimeDashboard.Infrastructure.Persistence;
using RealTimeDashboard.Infrastructure.Services;

namespace RealTimeDashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        AddDatabase(services, config);
        AddIdentity(services);
        AddRedis(services, config);
        AddAuth(services, config);

        services.AddScoped<IEventPublisher, RedisEventPublisher>();
        services.AddScoped<IDashboardStatsService, DashboardStatsService>();

        services.AddHostedService<BackgroundServices.OrderEventWorker>();

        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Data Source=dashboard.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (IsSqlServer(connectionString))
                options.UseSqlServer(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
    }

    // SQL Server connection strings use Server=/Data Source=tcp; SQLite uses a file path.
    private static bool IsSqlServer(string connectionString) =>
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase);

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
    }

    private static void AddRedis(IServiceCollection services, IConfiguration config)
    {
        var redisConnection = config["Redis:ConnectionString"] ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(redisConnection);
        options.AbortOnConnectFail = false; // keep retrying if Redis isn't up yet

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(options));
    }

    private static void AddAuth(IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
    }
}
