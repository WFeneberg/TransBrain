using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Abstractions;

public interface IVehicleRepository
{
    Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken);

    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
