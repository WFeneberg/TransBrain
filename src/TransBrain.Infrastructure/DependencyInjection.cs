using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Assistant;
using TransBrain.Infrastructure.Billing;
using TransBrain.Infrastructure.Imports;
using TransBrain.Infrastructure.Persistence.Caching;
using TransBrain.Infrastructure.Persistence.InvoiceNumbering;
using TransBrain.Infrastructure.Persistence.Maintenance;
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
        services.AddScoped<IInvoiceDesignRepository, InvoiceDesignRepository>();
        services.AddScoped<IInvoiceNumberGenerator, SequentialInvoiceNumberGenerator>();
        services.AddSingleton<IInvoicePdfRenderer, MigraDocInvoicePdfRenderer>();
        services.AddSingleton<IXRechnungWriter, UblXRechnungWriter>();
        services.AddScoped<IGeoOverrideRepository, GeoOverrideRepository>();
        services.AddScoped<ITenantDataMaintenance, PostgresTenantDataMaintenance>();
        services.AddScoped<ITenantDirectory, PostgresTenantDirectory>();
        services.AddScoped<IImportSessionRepository, ImportSessionRepository>();
        services.AddSingleton<IImportSourceParser, ImportSourceParser>();
        services.AddHttpClient(RestApiImportReader.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            // Some public APIs refuse anonymous clients; identify the importer honestly.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TransBrain-Import/1.0");
        });
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
        services.AddSingleton<IAssistantConfigurationStore, HermesEnvAssistantConfigurationStore>();
        services.AddSingleton<IAssistantFileDrop, HermesVolumeFileDrop>();
        // ServiceDefaults wraps every HttpClient in the standard resilience handler (30 s total
        // timeout, 10 s per attempt, retries). Agent turns legitimately run for minutes, and
        // retrying a chat POST would start a duplicate agent turn — so the hermes client opts out
        // via RemoveAllResilienceHandlers (experimental, hence the pragma, but purpose-built for
        // this); client.Timeout below is the only bound.
#pragma warning disable EXTEXP0001
        services.AddHttpClient<IAssistantGateway, HermesAgentClient>((provider, client) =>
        {
            HermesAgentOptions options = provider.GetRequiredService<IOptions<HermesAgentOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                // No BaseAddress when unprovisioned; the client then reports the assistant missing.
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            }
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
            client.Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds));
        })
        .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        return services;
    }
}
