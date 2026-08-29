using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>
/// Wraps <see cref="InMemoryVehicleRepository"/> and counts <see cref="ListAsync"/> calls, so
/// caching tests can assert the repository was (or was not) hit without inspecting cache internals.
/// </summary>
public sealed class CountingVehicleRepository : IVehicleRepository
{
    private readonly InMemoryVehicleRepository _inner = new();

    public int ListCallCount { get; private set; }

    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, Guid? excludingId, CancellationToken cancellationToken)
        => _inner.ExistsByLicensePlateAsync(plate, excludingId, cancellationToken);

    public Task<Result<Vehicle>> AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
        => _inner.AddAsync(vehicle, cancellationToken);

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Vehicle>> ListAsync(
        int skip, int take, VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken)
    {
        ListCallCount++;
        return _inner.ListAsync(skip, take, status, type, cancellationToken);
    }

    public Task<int> CountAsync(VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken)
        => _inner.CountAsync(status, type, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _inner.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken)
        => _inner.RemoveAsync(vehicle, cancellationToken);
}
