using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryTourRepository : ITourRepository
{
    private readonly List<Tour> _tours = [];

    public IReadOnlyList<Tour> Tours => _tours;

    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Set to make AddAsync answer the Conflict the real repository produces when the
    /// (TourDate, VehicleId) or (TourDate, DriverId) unique index rejects a double booking.
    /// The fake cannot enforce an index, so the handler test says which outcome it wants.
    /// </summary>
    public Error? AddConflict { get; set; }

    public void Seed(params Tour[] tours) => _tours.AddRange(tours);

    public void ResetSaveCount() => SaveChangesCallCount = 0;

    public Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken)
    {
        if (AddConflict is not null)
        {
            return Task.FromResult(Result<Tour>.Failure(AddConflict));
        }

        _tours.Add(tour);
        return Task.FromResult(Result<Tour>.Success(tour));
    }

    public Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_tours.SingleOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Tour>>(
            Filter(tourDate, vehicleId, driverId)
                .OrderBy(t => t.TourDate)
                .ThenBy(t => t.Id)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken)
        => Task.FromResult(Filter(tourDate, vehicleId, driverId).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    private IEnumerable<Tour> Filter(DateOnly? tourDate, Guid? vehicleId, Guid? driverId)
    {
        IEnumerable<Tour> query = _tours;

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
