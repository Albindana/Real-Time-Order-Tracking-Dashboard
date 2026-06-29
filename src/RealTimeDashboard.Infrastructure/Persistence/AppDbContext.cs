using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Domain.Entities;

namespace RealTimeDashboard.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Category).IsRequired().HasMaxLength(100);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.HasIndex(p => p.Category);
        });

        builder.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            e.Property(o => o.CustomerName).HasMaxLength(200);
            e.Property(o => o.CustomerEmail).HasMaxLength(256);
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.Status).HasConversion<int>();

            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.CustomerId);
            e.HasIndex(o => o.CreatedAt);
            e.HasIndex(o => o.Status);

            e.HasMany(o => o.Items)
                .WithOne(i => i.Order!)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });
    }
}
