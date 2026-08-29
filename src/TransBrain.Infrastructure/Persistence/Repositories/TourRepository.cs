using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class TourRepository(TransBrainDbContext context) : ITourRepository
{
    // PostgreSQL error code for unique_violation.
    private const string UniqueViolation = "23505";

    private const string VehicleIndex = "ix_tours_date_vehicle_unique";

    public async Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken)
    {
        await context.Tours.AddAsync(tour, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return tour;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: UniqueViolation } postgres)
        {
            context.Entry(tour).State = EntityState.Detached;

            // Naming which of the two is double-booked matters: "this tour conflicts" sends a
            // dispatcher looking at both the lorry and the driver.
            return postgres.ConstraintName == VehicleIndex
                ? Error.Conflict(
                    "Tour.VehicleAlreadyBooked",
                    $"That vehicle already has a tour on {tour.TourDate:yyyy-MM-dd}.")
                : Error.Conflict(
                    "Tour.DriverAlreadyBooked",
                    $"That driver already has a tour on {tour.TourDate:yyyy-MM-dd}.");
        }
    }

    // Stops are an owned collection, so they load with the tour; no Include is needed. They are
    // tracked deliberately - AssignOrder and RemoveOrder mutate them.
    public Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Tours.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => await Filter(tourDate, vehicleId, driverId)
            .OrderBy(t => t.TourDate)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Filter(tourDate, vehicleId, driverId).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    private IQueryable<Tour> Filter(DateOnly? tourDate, Guid? vehicleId, Guid? driverId)
    {
        IQueryable<Tour> query = context.Tours;

        if (tourDate is not null)
        {
            query = query.Where(t => t.TourDate == tourDate);
        }

        if (vehicleId is not null)
        {
            query = query.Where(t => t.VehicleId == vehicleId);
        }

        if (driverId is not null)
        {
            query = query.Where(t => t.DriverId == driverId);
        }

        return query;
    }
}
