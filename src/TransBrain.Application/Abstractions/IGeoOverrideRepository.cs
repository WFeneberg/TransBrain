using TransBrain.Domain.Routing;

namespace TransBrain.Application.Abstractions;

public interface IGeoOverrideRepository
{
    Task<GeoOverride?> GetByKeyAsync(string addressKey, CancellationToken cancellationToken);

    Task<GeoOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GeoOverride>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(GeoOverride geoOverride, CancellationToken cancellationToken);

    Task RemoveAsync(GeoOverride geoOverride, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every distinct address the system currently routes to: delivery locations plus consignor
    /// and consignee addresses of transport orders, aggregated per address.
    /// </summary>
    Task<IReadOnlyList<Features.Geodata.UsedAddress>> ListUsedAddressesAsync(CancellationToken cancellationToken);
}
