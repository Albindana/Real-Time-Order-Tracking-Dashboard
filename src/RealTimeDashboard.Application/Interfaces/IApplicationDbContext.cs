using Microsoft.EntityFrameworkCore;
using RealTimeDashboard.Domain.Entities;

namespace RealTimeDashboard.Application.Interfaces;

/// <summary>
/// Abstraction over the EF Core context so Application services depend on data access
/// without taking a reference to the concrete provider (SQLite/SQL Server).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
