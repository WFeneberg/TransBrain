using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Abstractions;

public interface IDriverRepository
{
    Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken);

    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Driver>> ListAsync(int skip, int take, DriverStatus? status, CancellationToken cancellationToken);

    Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Driver driver, CancellationToken cancellationToken);
}
