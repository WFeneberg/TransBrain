using TransBrain.Application.Abstractions;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly List<Vehicle> _vehicles = [];

    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    public void Seed(params Vehicle[] vehicles) => _vehicles.AddRange(vehicles);

    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken)
        => Task.FromResult(_vehicles.Any(v => v.LicensePlate == plate));

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        _vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Vehicle>>(
            _vehicles.OrderBy(v => v.LicensePlate.Value).Skip(skip).Take(take).ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_vehicles.Count);
}
