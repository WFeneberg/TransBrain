using TransBrain.Domain.Common;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Abstractions;

public interface ITourRepository
{
    /// <summary>
    /// Persists a new tour. Returns a <see cref="ErrorType.Conflict"/> when the database's
    /// unique index rejects a second tour for the same vehicle or driver on the same date —
    /// that rule cannot live in the domain, because uniqueness is not something one object
    /// can see.
    /// </summary>
    Task<Result<Tour>> AddAsync(Tour tour, CancellationToken cancellationToken);

    Task<Tour?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tour>> ListAsync(
        int skip,
        int take,
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(
        DateOnly? tourDate,
        Guid? vehicleId,
        Guid? driverId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
