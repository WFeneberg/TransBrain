using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Abstractions;

public interface IVehicleRepository
{
    /// <param name="excludingId">
    /// Ignore this vehicle when checking uniqueness, so updating a vehicle without changing
    /// its plate does not collide with itself.
    /// </param>
    Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, Guid? excludingId, CancellationToken cancellationToken);

    Task<Result<Vehicle>> AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(
        int skip, int take, VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken);

    Task<int> CountAsync(VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken);
}
