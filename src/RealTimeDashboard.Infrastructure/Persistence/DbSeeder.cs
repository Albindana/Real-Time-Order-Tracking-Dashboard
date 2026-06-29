using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Domain.Enums;
using RealTimeDashboard.Infrastructure.Auth;

namespace RealTimeDashboard.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        var (admin, customer) = await SeedUsersAsync(userManager);
        await SeedProductsAsync(db);
        await SeedOrdersAsync(db, customer);

        logger.LogInformation("Database seeding complete.");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { AuthService.AdminRole, AuthService.CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task<(AppUser admin, AppUser customer)> SeedUsersAsync(UserManager<AppUser> userManager)
    {
        var admin = await EnsureUserAsync(userManager, "admin@dashboard.com", "Admin123!",
            "Ada", "Admin", AuthService.AdminRole);
        var customer = await EnsureUserAsync(userManager, "customer@dashboard.com", "Customer123!",
            "Carl", "Customer", AuthService.CustomerRole);

        return (admin, customer);
    }

    private static async Task<AppUser> EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email, string password, string firstName, string lastName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow
        };

        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task SeedProductsAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync()) return;

        var products = new List<Product>
        {
            new() { Name = "Mechanical Keyboard",   Category = "Electronics", Price = 129.99m, StockQuantity = 40 },
            new() { Name = "Wireless Mouse",        Category = "Electronics", Price = 49.99m,  StockQuantity = 60 },
            new() { Name = "27\" 4K Monitor",       Category = "Electronics", Price = 379.00m, StockQuantity = 15 },
            new() { Name = "USB-C Hub",             Category = "Electronics", Price = 39.50m,  StockQuantity = 3  }, // low stock
            new() { Name = "Ergonomic Chair",       Category = "Furniture",   Price = 249.00m, StockQuantity = 12 },
            new() { Name = "Standing Desk",         Category = "Furniture",   Price = 459.00m, StockQuantity = 8  },
            new() { Name = "Desk Lamp",             Category = "Furniture",   Price = 34.99m,  StockQuantity = 3  }, // low stock
            new() { Name = "Notebook (A5)",         Category = "Stationery",  Price = 7.99m,   StockQuantity = 200 },
            new() { Name = "Gel Pen Pack",          Category = "Stationery",  Price = 4.50m,   StockQuantity = 150 },
            new() { Name = "Sticky Notes",          Category = "Stationery",  Price = 3.25m,   StockQuantity = 120 },
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    private static async Task SeedOrdersAsync(AppDbContext db, AppUser customer)
    {
        if (await db.Orders.AnyAsync()) return;

        var products = await db.Products.ToListAsync();
        var statuses = new[]
        {
            OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped,
            OrderStatus.Delivered, OrderStatus.Cancelled
        };

        var orders = new List<Order>();
        for (var i = 0; i < 15; i++)
        {
            // Deterministic spread across the last 7 days and rotating statuses (no RNG — keeps seeds stable).
            var createdAt = DateTime.UtcNow.AddDays(-(i % 7)).AddHours(-(i * 2));
            var status = statuses[i % statuses.Length];

            var order = new Order
            {
                OrderNumber = $"ORD-{createdAt.Year}-{i + 1:D5}",
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email!,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            var lineCount = (i % 3) + 1;
            for (var j = 0; j < lineCount; j++)
            {
                var product = products[(i + j) % products.Count];
                var quantity = (j % 2) + 1;
                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }

            order.ItemCount = order.Items.Sum(x => x.Quantity);
            order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);
            orders.Add(order);
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }
}
