using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Abstractions;

public interface IVehicleCatalogRepository
{
    /// <summary>All entries of one catalog, active and inactive, ordered by name.</summary>
    Task<IReadOnlyList<VehicleCatalogEntry>> ListAsync(VehicleCatalogKind kind, CancellationToken cancellationToken);

    Task<VehicleCatalogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Looks an entry up by code, ignoring case.</summary>
    Task<VehicleCatalogEntry?> FindByCodeAsync(
        VehicleCatalogKind kind, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Number of vehicles per code that currently refer to an entry of this catalog; for
    /// <see cref="VehicleCatalogKind.CustomerClass"/> the number of customers.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountUsageAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken);

    Task<Result<VehicleCatalogEntry>> AddAsync(VehicleCatalogEntry entry, CancellationToken cancellationToken);

    Task RemoveAsync(VehicleCatalogEntry entry, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}