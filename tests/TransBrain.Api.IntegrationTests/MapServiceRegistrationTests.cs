using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Infrastructure;
using TransBrain.Infrastructure.Routing;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The choice between the self-hosted containers and the public services lives in the HttpClient
/// registrations, not in the clients: these tests pin down where each configured client points
/// and which identifying headers it carries in each mode.
/// </summary>
public sealed class MapServiceRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RoutingSelfHostedDefault_PointsAtContainerWithoutCredentials()
    {
        HttpClient client = CreateConfiguredClient(nameof(OpenRouteServiceClient), new Dictionary<string, string?>
        {
            ["OpenRouteService:BaseUrl"] = "http://container:8082/ors/"
        });

        client.BaseAddress.Should().Be(new Uri("http://container:8082/ors/"));
        client.DefaultRequestHeaders.Contains("Authorization").Should().BeFalse();
    }

    [Fact]
    public void AddInfrastructure_RoutingOnline_PointsAtPublicApiWithKey()
    {
        HttpClient client = CreateConfiguredClient(nameof(OpenRouteServiceClient), new Dictionary<string, string?>
        {
            ["OpenRouteService:Mode"] = "Online",
            ["OpenRouteService:ApiKey"] = "test-key"
        });

        client.BaseAddress.Should().Be(new Uri("https://api.openrouteservice.org/"));
        client.DefaultRequestHeaders.GetValues("Authorization").Should().Equal("test-key");
    }

    [Fact]
    public void AddInfrastructure_RoutingOnlineWithoutKey_SendsNoAuthorizationHeader()
    {
        HttpClient client = CreateConfiguredClient(nameof(OpenRouteServiceClient), new Dictionary<string, string?>
        {
            ["OpenRouteService:Mode"] = "Online"
        });

        client.BaseAddress.Should().Be(new Uri("https://api.openrouteservice.org/"));
        client.DefaultRequestHeaders.Contains("Authorization").Should().BeFalse();
    }

    [Fact]
    public void AddInfrastructure_GeocodingSelfHostedDefault_PointsAtContainerWithoutUserAgent()
    {
        HttpClient client = CreateConfiguredClient(nameof(NominatimGeocoder), new Dictionary<string, string?>
        {
            ["Nominatim:BaseUrl"] = "http://container:8088/"
        });

        client.BaseAddress.Should().Be(new Uri("http://container:8088/"));
        client.DefaultRequestHeaders.Contains("User-Agent").Should().BeFalse();
    }

    [Fact]
    public void AddInfrastructure_GeocodingOnline_PointsAtPublicInstanceAndIdentifiesItself()
    {
        HttpClient client = CreateConfiguredClient(nameof(NominatimGeocoder), new Dictionary<string, string?>
        {
            ["Nominatim:Mode"] = "Online"
        });

        client.BaseAddress.Should().Be(new Uri("https://nominatim.openstreetmap.org/"));
        // The public instance's usage policy requires an identifying User-Agent.
        string userAgent = string.Join(' ', client.DefaultRequestHeaders.GetValues("User-Agent"));
        userAgent.Should().Contain("TransBrain");
    }

    private static HttpClient CreateConfiguredClient(string clientName, Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.Configure<OpenRouteServiceOptions>(configuration.GetSection(OpenRouteServiceOptions.SectionName));
        services.Configure<NominatimOptions>(configuration.GetSection(NominatimOptions.SectionName));
        services.AddInfrastructure();

        // Disposing the provider only tears down the handler; the metadata under test stays readable.
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
    }
}
