using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Application.Services;

namespace RealTimeDashboard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
