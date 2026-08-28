using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class VehicleRepository(TransBrainDbContext context) : IVehicleRepository
{
    // PostgreSQL error code for unique_violation.
    private const string UniqueViolation = "23505";

    public Task<bool> ExistsByLicensePlateAsync(LicensePlate plate, CancellationToken cancellationToken)
        => context.Vehicles.AnyAsync(v => v.LicensePlate == plate, cancellationToken);

    public async Task<Result<Vehicle>> AddAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        await context.Vehicles.AddAsync(vehicle, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return vehicle;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            context.Entry(vehicle).State = EntityState.Detached;
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{vehicle.LicensePlate.Value}' already exists.");
        }
    }

    // Ordering agreement with InMemoryVehicleRepository (StringComparer.Ordinal) rests on the "C"
    // collation configured on the LicensePlate column in VehicleConfiguration, not on convention.
    public async Task<IReadOnlyList<Vehicle>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => await context.Vehicles
            .OrderBy(v => v.LicensePlate)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => context.Vehicles.CountAsync(cancellationToken);
}
