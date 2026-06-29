using Microsoft.EntityFrameworkCore;
using RealTimeDashboard.Infrastructure.Persistence;

namespace RealTimeDashboard.Tests;

/// <summary>Builds an isolated in-memory AppDbContext per test.</summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
