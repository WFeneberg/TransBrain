using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Routing;

/// <remarks>
/// Geocoding is paid per call and rate limited, and an address rarely moves. A resolved point is
/// cached under the address itself, so the map and the route planner share one lookup. Only
/// successes are cached: a failure may be a transient outage that the next request should retry.
/// </remarks>
internal static class LocationResolver
{
    public static async Task<Result<GeoPoint>> ResolveAsync(
        Address address,
        IGeocoder geocoder,
        ICacheService cache,
        CancellationToken cancellationToken)
    {
        string key = $"geocode:{address.Country}|{address.PostalCode}|{address.City}|{address.Street}"
            .ToLowerInvariant();

        CachedPoint? cached = await cache.GetAsync<CachedPoint>(key, cancellationToken);
        if (cached is not null)
        {
            return GeoPoint.Create(cached.Latitude, cached.Longitude);
        }

        Result<GeoPoint> resolved = await geocoder.GeocodeAsync(address, cancellationToken);
        if (resolved.IsSuccess)
        {
            await cache.SetAsync(
                key,
                new CachedPoint(resolved.Value.Latitude, resolved.Value.Longitude),
                cancellationToken);
        }

        return resolved;
    }

    // GeoPoint keeps its constructor private, so the cache stores this plain shape instead.
    private sealed record CachedPoint(double Latitude, double Longitude);
}
