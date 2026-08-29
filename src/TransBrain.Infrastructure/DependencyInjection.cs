using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Persistence.Caching;
using TransBrain.Infrastructure.Persistence.Repositories;

namespace TransBrain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }
}
