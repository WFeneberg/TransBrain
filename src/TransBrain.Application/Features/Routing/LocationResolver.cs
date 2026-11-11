using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Routing;

/// <remarks>
/// A manual override is checked first: an operator-maintained position must win over anything the
/// geocoder once answered, and the cache only ever stores geocoder results, so removing an
/// override cleanly falls back to the automatic path. Geocoding is paid per call and rate
/// limited, and an address rarely moves. A resolved point is cached under the address itself, so
/// the map and the route planner share one lookup. Only successes are cached: a failure may be a
/// transient outage that the next request should retry.
/// </remarks>
internal static class LocationResolver
{
    public static async Task<Result<GeoPoint>> ResolveAsync(
        Address address,
        IGeoOverrideRepository overrides,
        IGeocoder geocoder,
        ICacheService cache,
        CancellationToken cancellationToken)
    {
        GeoOverride? manual = await overrides.GetByKeyAsync(GeoOverride.BuildKey(address), cancellationToken);
        if (manual is not null)
        {
            return manual.Position;
        }

        string key = GeocodeCache.Key(address);

        GeocodeCache.CachedPoint? cached = await cache.GetAsync<GeocodeCache.CachedPoint>(key, cancellationToken);
        if (cached is not null)
        {
            return GeoPoint.Create(cached.Latitude, cached.Longitude);
        }

        Result<GeoPoint> resolved = await geocoder.GeocodeAsync(address, cancellationToken);
        if (resolved.IsSuccess)
        {
            await cache.SetAsync(
                key,
                new GeocodeCache.CachedPoint(resolved.Value.Latitude, resolved.Value.Longitude),
                cancellationToken);
        }

        return resolved;
    }
}
