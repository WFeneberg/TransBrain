using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Geodata;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class GeoOverrideRepository(TransBrainDbContext context) : IGeoOverrideRepository
{
    public Task<GeoOverride?> GetByKeyAsync(string addressKey, CancellationToken cancellationToken)
        => context.GeoOverrides.SingleOrDefaultAsync(o => o.AddressKey == addressKey, cancellationToken);

    public Task<GeoOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.GeoOverrides.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GeoOverride>> ListAsync(CancellationToken cancellationToken)
        => await context.GeoOverrides
            .AsNoTracking()
            .OrderBy(o => o.AddressKey)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(GeoOverride geoOverride, CancellationToken cancellationToken)
        => await context.GeoOverrides.AddAsync(geoOverride, cancellationToken);

    public Task RemoveAsync(GeoOverride geoOverride, CancellationToken cancellationToken)
    {
        context.GeoOverrides.Remove(geoOverride);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<UsedAddress>> ListUsedAddressesAsync(CancellationToken cancellationToken)
    {
        // Three slim projections, aggregated in memory: the distinct-address count of a haulier's
        // master data is small, and grouping case-insensitively in the database would force
        // lower() over every column with no index to help it.
        var locations = await context.CustomerLocations
            .AsNoTracking()
            .Select(l => new AddressUse(
                l.Address.Street, l.Address.PostalCode, l.Address.City, l.Address.Country, false))
            .ToListAsync(cancellationToken);

        var consignors = await context.TransportOrders
            .AsNoTracking()
            .Select(o => new AddressUse(
                o.Consignor.Street, o.Consignor.PostalCode, o.Consignor.City, o.Consignor.Country, true))
            .ToListAsync(cancellationToken);

        var consignees = await context.TransportOrders
            .AsNoTracking()
            .Select(o => new AddressUse(
                o.Consignee.Street, o.Consignee.PostalCode, o.Consignee.City, o.Consignee.Country, true))
            .ToListAsync(cancellationToken);

        return locations.Concat(consignors).Concat(consignees)
            .GroupBy(use => GeoOverride.BuildKey(use.Street, use.PostalCode, use.City, use.Country))
            .Select(group => new UsedAddress(
                group.First().Street,
                group.First().PostalCode,
                group.First().City,
                group.First().Country,
                group.Count(use => use.FromOrder),
                group.Count(use => !use.FromOrder)))
            .ToList();
    }

    private sealed record AddressUse(string Street, string PostalCode, string City, string Country, bool FromOrder);
}
