using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly List<Vehicle> _vehicles = [];

    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(params Vehicle[] vehicles) => _vehicles.AddRange(vehicles);

    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, Guid? excludingId, CancellationToken cancellationToken)
        => Task.FromResult(_vehicles.Any(v => v.LicensePlate == plate && (excludingId == null || v.Id != excludingId)));

    public Task<Result<Vehicle>> AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        _vehicles.Add(vehicle);
        return Task.FromResult(Result<Vehicle>.Success(vehicle));
    }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_vehicles.SingleOrDefault(v => v.Id == id));

    // Ordinal, not culture-sensitive, comparison: the later EF-backed repository must order
    // under a matching ordinal collation so the fake and the real repository cannot drift.
    public Task<IReadOnlyList<Vehicle>> ListAsync(
        int skip, int take, VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Vehicle>>(
            Filter(status, type)
                .OrderBy(v => v.LicensePlate.Value, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(VehicleStatus? status, VehicleType? type, CancellationToken cancellationToken)
        => Task.FromResult(Filter(status, type).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        _vehicles.Remove(vehicle);
        return Task.CompletedTask;
    }

    private IEnumerable<Vehicle> Filter(VehicleStatus? status, VehicleType? type)
        => _vehicles
            .Where(v => status is null || v.Status == status)
            .Where(v => type is null || v.Type == type);
}
