using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Geodata.ListGeodata;

/// <remarks>
/// Deliberately uncached: this is a maintenance view whose point is to show the current state of
/// the geocode cache and the overrides, and it is opened rarely compared to the lists it reports
/// on.
/// </remarks>
internal sealed class ListGeodataQueryHandler(IGeoOverrideRepository repository, ICacheService cache)
    : IQueryHandler<ListGeodataQuery, GeodataEntryResponse[]>
{
    public async Task<Result<GeodataEntryResponse[]>> Handle(
        ListGeodataQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UsedAddress> used = await repository.ListUsedAddressesAsync(cancellationToken);
        IReadOnlyList<GeoOverride> overrides = await repository.ListAsync(cancellationToken);
        Dictionary<string, GeoOverride> overrideByKey = overrides.ToDictionary(o => o.AddressKey);

        List<GeodataEntryResponse> entries = [];
        HashSet<string> usedKeys = [];

        foreach (UsedAddress address in used)
        {
            string key = GeoOverride.BuildKey(address.Street, address.PostalCode, address.City, address.Country);
            usedKeys.Add(key);

            if (overrideByKey.TryGetValue(key, out GeoOverride? manual))
            {
                entries.Add(Entry(address, manual, inUse: true));
                continue;
            }

            GeocodeCache.CachedPoint? cached = await cache.GetAsync<GeocodeCache.CachedPoint>(
                GeocodeCache.Key(address.Street, address.PostalCode, address.City, address.Country),
                cancellationToken);

            entries.Add(new GeodataEntryResponse(
                address.Street,
                address.PostalCode,
                address.City,
                address.Country,
                cached?.Latitude,
                cached?.Longitude,
                cached is null ? GeodataSources.Unresolved : GeodataSources.Geocoded,
                OverrideId: null,
                address.OrderCount,
                address.LocationCount,
                InUse: true,
                UpdatedAt: null));
        }

        // Overrides for addresses no longer in use stay visible, so an operator can clean them up.
        foreach (GeoOverride orphan in overrides.Where(o => !usedKeys.Contains(o.AddressKey)))
        {
            entries.Add(Entry(
                new UsedAddress(orphan.Street, orphan.PostalCode, orphan.City, orphan.Country, 0, 0),
                orphan,
                inUse: false));
        }

        return entries
            .OrderBy(e => e.Country, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.City, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Street, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static GeodataEntryResponse Entry(UsedAddress address, GeoOverride manual, bool inUse)
        => new(
            address.Street,
            address.PostalCode,
            address.City,
            address.Country,
            manual.Position.Latitude,
            manual.Position.Longitude,
            GeodataSources.Manual,
            manual.Id,
            address.OrderCount,
            address.LocationCount,
            inUse,
            manual.UpdatedAt);
}
