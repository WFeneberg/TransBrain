using System.Net;
using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;
using TransBrain.Infrastructure.Routing;

namespace TransBrain.Api.IntegrationTests;

public sealed class OpenRouteServiceClientTests
{
    private static readonly GeoPoint[] Waypoints =
    [
        GeoPoint.Create(48.137, 11.575).Value,
        GeoPoint.Create(48.05, 10.87).Value
    ];

    [Fact]
    public async Task PlanAsync_GeoJsonAnswer_ReturnsSegmentsAndGeometry()
    {
        const string json = """
            {"features":[{"geometry":{"coordinates":[[11.575,48.137],[10.87,48.05]]},
            "properties":{"segments":[{"distance":61000.5,"duration":3600}]}}]}
            """;
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        Result<RoadRoute> result = await CreateClient(handler).PlanAsync(Waypoints, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Segments.Should().Equal(new RouteSegment(61000.5, TimeSpan.FromHours(1)));
        result.Value.Geometry.Should().Equal(Waypoints);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/ors/v2/directions/driving-hgv/geojson");
        handler.LastRequest.Headers.Contains("Authorization").Should().BeFalse();
    }

    [Fact]
    public async Task PlanAsync_ServiceUnreachable_ReturnsUnavailable()
    {
        StubHandler handler = new(_ => throw new HttpRequestException("refused"));

        Result<RoadRoute> result = await CreateClient(handler).PlanAsync(Waypoints, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Routing.Unreachable");
    }

    private static OpenRouteServiceClient CreateClient(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ors.test/ors/") });

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
