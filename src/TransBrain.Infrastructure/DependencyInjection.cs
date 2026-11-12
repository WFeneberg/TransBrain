using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Billing;
using TransBrain.Infrastructure.Persistence.Caching;
using TransBrain.Infrastructure.Persistence.InvoiceNumbering;
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
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceNumberGenerator, SequentialInvoiceNumberGenerator>();
        services.AddSingleton<IInvoicePdfRenderer, MigraDocInvoicePdfRenderer>();
        services.AddSingleton<IXRechnungWriter, UblXRechnungWriter>();
        services.AddScoped<IGeoOverrideRepository, GeoOverrideRepository>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddHttpClient<OpenRouteServiceClient>((provider, client) =>
        {
            OpenRouteServiceOptions options = provider.GetRequiredService<IOptions<OpenRouteServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.EffectiveBaseUrl);
            if (options.UsesOnlineService && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                // The public gateway expects the bare key, without an auth scheme prefix.
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.ApiKey);
            }
        });
        services.AddHttpClient<NominatimGeocoder>((provider, client) =>
        {
            NominatimOptions options = provider.GetRequiredService<IOptions<NominatimOptions>>().Value;
            client.BaseAddress = new Uri(options.EffectiveBaseUrl);
            if (options.UsesOnlineService)
            {
                // The public instance's usage policy requires an identifying User-Agent.
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }
        });
        services.AddTransient<IGeocoder>(provider => provider.GetRequiredService<NominatimGeocoder>());
        services.AddTransient<IRoutePlanner>(provider => provider.GetRequiredService<OpenRouteServiceClient>());
        services.AddTransient<IMapDataStatusProvider, MapDataStatusProvider>();
        return services;
    }
}
