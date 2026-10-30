using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Routing;

/// <remarks>
/// OpenRouteService answers both geocoding and truck routing behind one API key. Routing uses the
/// driving-hgv profile, so the road network excludes roads closed to heavy goods vehicles.
/// Without a key every call answers Unavailable instead of guessing, so the map shows an honest
/// error rather than a route that ignores truck restrictions.
/// </remarks>
internal sealed class OpenRouteServiceClient(HttpClient httpClient, IOptions<OpenRouteServiceOptions> options)
    : IGeocoder, IRoutePlanner
{
    private const string TruckProfile = "driving-hgv";

    private readonly OpenRouteServiceOptions _options = options.Value;

    public async Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return NotConfigured();
        }

        string text = $"{address.Street}, {address.PostalCode} {address.City}, {address.Country}";
        Result<GeocodeResponse> response = await GetAsync<GeocodeResponse>(
            $"geocode/search?text={Uri.EscapeDataString(text)}&size=1",
            cancellationToken);

        if (!response.IsSuccess)
        {
            return response.Error!;
        }

        if (response.Value.Features.Count == 0)
        {
            return Error.NotFound("Geocoding.NoMatch", $"No place found for '{text}'.");
        }

        double[] coordinates = response.Value.Features[0].Geometry.Coordinates;
        return GeoPoint.Create(latitude: coordinates[1], longitude: coordinates[0]);
    }

    public async Task<Result<RoadRoute>> PlanAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return NotConfigured();
        }

        // ORS takes [longitude, latitude] pairs, the reverse of how GeoPoint reads.
        object body = new
        {
            coordinates = waypoints.Select(point => new[] { point.Longitude, point.Latitude }).ToArray(),
            instructions = false
        };

        Result<DirectionsResponse> response = await PostAsync<DirectionsResponse>(
            $"v2/directions/{TruckProfile}/geojson",
            body,
            cancellationToken);

        if (!response.IsSuccess)
        {
            return response.Error!;
        }

        LineFeature feature = response.Value.Features[0];
        List<RouteSegment> segments = feature.Properties.Segments
            .Select(segment => new RouteSegment(segment.Distance, TimeSpan.FromSeconds(segment.Duration)))
            .ToList();

        if (segments.Count != waypoints.Count - 1)
        {
            return Error.Unavailable(
                "Routing.Inconsistent",
                $"OpenRouteService returned {segments.Count} segments for {waypoints.Count} waypoints.");
        }

        List<GeoPoint> geometry = [];
        foreach (double[] coordinate in feature.Geometry.Coordinates)
        {
            Result<GeoPoint> point = GeoPoint.Create(latitude: coordinate[1], longitude: coordinate[0]);
            if (!point.IsSuccess)
            {
                return point.Error!;
            }

            geometry.Add(point.Value);
        }

        return new RoadRoute(segments, geometry);
    }

    private async Task<Result<T>> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
        where T : class
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<Result<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        where T : class
    {
        request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable(
                    "Routing.ServiceError",
                    $"OpenRouteService answered HTTP {(int)response.StatusCode}.");
            }

            T? body = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return body is null
                ? Error.Unavailable("Routing.EmptyResponse", "OpenRouteService answered without a body.")
                : body;
        }
        catch (HttpRequestException)
        {
            return Error.Unavailable("Routing.Unreachable", "OpenRouteService could not be reached.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient raises TaskCanceledException on its own timeout. Only a caller's cancellation is
            // rethrown, so a slow provider surfaces as Unavailable instead of a 500.
            return Error.Unavailable("Routing.Timeout", "OpenRouteService did not answer in time.");
        }
    }

    private static Error NotConfigured() => Error.Unavailable(
        "Routing.NotConfigured",
        "Routing is not configured. Set OpenRouteService:ApiKey.");

    private sealed record GeocodeResponse(IReadOnlyList<PointFeature> Features);

    private sealed record PointFeature(PointGeometry Geometry);

    private sealed record PointGeometry(double[] Coordinates);

    private sealed record DirectionsResponse(IReadOnlyList<LineFeature> Features);

    private sealed record LineFeature(LineGeometry Geometry, LineProperties Properties);

    private sealed record LineGeometry(IReadOnlyList<double[]> Coordinates);

    private sealed record LineProperties(IReadOnlyList<SegmentSummary> Segments);

    private sealed record SegmentSummary(double Distance, double Duration);
}

/// <summary>Settings for the OpenRouteService adapter. The key comes from configuration, never from code.</summary>
public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org/";
}
