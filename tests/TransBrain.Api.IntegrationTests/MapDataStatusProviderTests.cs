using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Routing;

namespace TransBrain.Api.IntegrationTests;

public sealed class MapDataStatusProviderTests
{
    [Fact]
    public async Task GetRoutingStatusAsync_SelfHostedReady_ReportsAvailableWithOsmDate()
    {
        StubHandler handler = new(request => request.RequestUri!.AbsolutePath switch
        {
            "/v2/health" => Json("""{"status":"ready"}"""),
            "/v2/status" => Json("""{"profiles":{"driving-hgv":{"storages":{"osm_date":"2026-01-15T00:00:00Z"}}}}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        MapServiceStatus status = await CreateProvider(handler, new OpenRouteServiceOptions())
            .GetRoutingStatusAsync(CancellationToken.None);

        status.Available.Should().BeTrue();
        status.OsmDataUpdatedAt.Should().Be(DateTimeOffset.Parse("2026-01-15T00:00:00Z"));
    }

    // The empty probe body is rejected by a working service with 400, which proves both
    // reachability and an accepted key; auth answers prove the opposite.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task GetRoutingStatusAsync_OnlineMode_MapsProbeAnswerToAvailability(
        HttpStatusCode answer,
        bool expectedAvailable)
    {
        StubHandler handler = new(_ => new HttpResponseMessage(answer)
        {
            Content = new StringContent("""{"error":"detail"}""", Encoding.UTF8, "application/json")
        });

        MapServiceStatus status = await CreateProvider(handler, OnlineOptions("test-key"))
            .GetRoutingStatusAsync(CancellationToken.None);

        status.Available.Should().Be(expectedAvailable);
        status.OsmDataUpdatedAt.Should().BeNull();
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v2/directions/driving-hgv/geojson");
    }

    [Fact]
    public async Task GetRoutingStatusAsync_OnlineModeWithoutKey_ReportsUnavailableWithoutProbing()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

        MapServiceStatus status = await CreateProvider(handler, OnlineOptions(apiKey: null))
            .GetRoutingStatusAsync(CancellationToken.None);

        status.Available.Should().BeFalse();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task GetRoutingStatusAsync_OnlineModeUnreachable_ReportsUnavailable()
    {
        StubHandler handler = new(_ => throw new HttpRequestException("refused"));

        MapServiceStatus status = await CreateProvider(handler, OnlineOptions("test-key"))
            .GetRoutingStatusAsync(CancellationToken.None);

        status.Available.Should().BeFalse();
    }

    private static OpenRouteServiceOptions OnlineOptions(string? apiKey) =>
        new() { Mode = MapServiceMode.Online, ApiKey = apiKey };

    private static MapDataStatusProvider CreateProvider(StubHandler handler, OpenRouteServiceOptions options) =>
        new(
            new StubHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("http://ors.test/") }),
            new ConfigurationBuilder().Build(),
            Options.Create(options));

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}
