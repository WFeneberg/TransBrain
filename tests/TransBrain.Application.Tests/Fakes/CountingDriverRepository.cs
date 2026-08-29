using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>
/// Wraps <see cref="InMemoryDriverRepository"/> and counts <see cref="ListAsync"/> calls, so
/// caching tests can assert the repository was (or was not) hit without inspecting cache internals.
/// </summary>
public sealed class CountingDriverRepository : IDriverRepository
{
    private readonly InMemoryDriverRepository _inner = new();

    public int ListCallCount { get; private set; }

    public Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken)
        => _inner.AddAsync(driver, cancellationToken);

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Driver>> ListAsync(
        int skip, int take, DriverStatus? status, CancellationToken cancellationToken)
    {
        ListCallCount++;
        return _inner.ListAsync(skip, take, status, cancellationToken);
    }

    public Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken)
        => _inner.CountAsync(status, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => _inner.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(Driver driver, CancellationToken cancellationToken)
        => _inner.RemoveAsync(driver, cancellationToken);
}
