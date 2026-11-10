using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Persistence.Caching;
using TransBrain.Infrastructure.Persistence.OrderNumbering;
using TransBrain.Infrastructure.Persistence.Repositories;
using TransBrain.Infrastructure.Routing;

namespace TransBrain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleCatalogRepository, VehicleCatalogRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ITransportOrderRepository, TransportOrderRepository>();
        services.AddScoped<IOrderNumberGenerator, SequentialOrderNumberGenerator>();
        services.AddScoped<ITourRepository, TourRepository>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddHttpClient<OpenRouteServiceClient>((provider, client) =>
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value.BaseUrl));
        services.AddHttpClient<NominatimGeocoder>((provider, client) =>
            client.BaseAddress = new Uri(
                provider.GetRequiredService<IConfiguration>()["Nominatim:BaseUrl"] ?? "http://localhost:8088/"));
        services.AddTransient<IGeocoder>(provider => provider.GetRequiredService<NominatimGeocoder>());
        services.AddTransient<IRoutePlanner>(provider => provider.GetRequiredService<OpenRouteServiceClient>());
        return services;
    }
}
