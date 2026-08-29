using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Abstractions;

public interface IDriverRepository
{
    Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken);

    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the driver bound to a Keycloak login. Backed by the unique filtered index on
    /// ExternalUserId, so it matches at most one driver — which is what spec §9's "a driver
    /// sees only their own tours" rule needs to be unambiguous.
    /// </summary>
    Task<Driver?> GetByExternalUserIdAsync(string externalUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Driver>> ListAsync(int skip, int take, DriverStatus? status, CancellationToken cancellationToken);

    Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Driver driver, CancellationToken cancellationToken);
}
