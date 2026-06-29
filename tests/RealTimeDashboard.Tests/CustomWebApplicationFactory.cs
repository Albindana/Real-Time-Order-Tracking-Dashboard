using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;
using RealTimeDashboard.Infrastructure.Persistence;

namespace RealTimeDashboard.Tests;

/// <summary>
/// Boots the real API pipeline (auth, controllers, serialization, EF, seeding) but swaps the
/// database for EF in-memory and Redis for a no-op mock so tests are self-contained.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"it-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            ReplaceDatabase(services);
            ReplaceRedis(services);
        });
    }

    private void ReplaceDatabase(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));
    }

    private static void ReplaceRedis(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IConnectionMultiplexer));
        if (descriptor is not null) services.Remove(descriptor);

        var db = new Mock<IDatabase>();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null); // force stats to recompute from the DB
        db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1L);
        db.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        var subscriber = new Mock<ISubscriber>();

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(db.Object);
        multiplexer.Setup(m => m.GetSubscriber(It.IsAny<object?>())).Returns(subscriber.Object);
        multiplexer.Setup(m => m.IsConnected).Returns(false);

        services.AddSingleton(multiplexer.Object);
    }
}
