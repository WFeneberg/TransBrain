using System.Net.Http.Json;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Routing;

/// <remarks>
/// Talks to OpenRouteService — either the self-hosted container the AppHost starts (default,
/// no API key, no external service) or the public api.openrouteservice.org, chosen by
/// <see cref="OpenRouteServiceOptions.Mode"/>; the registration in DependencyInjection points
/// the HttpClient accordingly. Routing uses the driving-hgv profile, so the road network
/// excludes roads closed to heavy goods vehicles. While the service is unreachable (container
/// down or still building its graph, key missing or quota spent online) every call answers
/// Unavailable instead of guessing, so the map shows an honest error rather than a route that
/// ignores truck restrictions. Geocoding is handled by NominatimGeocoder instead.
/// </remarks>
internal sealed class OpenRouteServiceClient(HttpClient httpClient) : IRoutePlanner
{
    private const string TruckProfile = "driving-hgv";

    public async Task<Result<RoadRoute>> PlanAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        // ORS takes [longitude, latitude] pairs, the reverse of how GeoPoint reads. Instructions stay
        // on: with "instructions": false ORS 10 leaves out the per-segment distances and durations.
        object body = new
        {
            coordinates = waypoints.Select(point => new[] { point.Longitude, point.Latitude }).ToArray()
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

    private async Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
        where T : class
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<Result<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        where T : class
    {
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

    private sealed record DirectionsResponse(IReadOnlyList<LineFeature> Features);

    private sealed record LineFeature(LineGeometry Geometry, LineProperties Properties);

    private sealed record LineGeometry(IReadOnlyList<double[]> Coordinates);

    private sealed record LineProperties(IReadOnlyList<SegmentSummary> Segments);

    private sealed record SegmentSummary(double Distance, double Duration);
}

/// <summary>
/// Settings for the OpenRouteService adapter. <see cref="Mode"/> picks between the self-hosted
/// container (default; the AppHost injects <see cref="BaseUrl"/>) and the public API
/// (<see cref="OnlineBaseUrl"/> plus <see cref="ApiKey"/>).
/// </summary>
public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";

    public MapServiceMode Mode { get; set; } = MapServiceMode.SelfHosted;

    /// <summary>Address of the self-hosted container, used when <see cref="Mode"/> is SelfHosted.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8082/ors/";

    /// <summary>Address of the public API, used when <see cref="Mode"/> is Online.</summary>
    public string OnlineBaseUrl { get; set; } = "https://api.openrouteservice.org/";

    /// <summary>
    /// API key for the public API, required when <see cref="Mode"/> is Online (free account at
    /// openrouteservice.org). Sent as the bare Authorization header the public gateway expects.
    /// </summary>
    public string? ApiKey { get; set; }

    public bool UsesOnlineService => Mode == MapServiceMode.Online;

    public string EffectiveBaseUrl => UsesOnlineService ? OnlineBaseUrl : BaseUrl;
}
