using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryDriverRepository : IDriverRepository
{
    private readonly List<Driver> _drivers = [];

    public IReadOnlyList<Driver> Drivers => _drivers;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(params Driver[] drivers) => _drivers.AddRange(drivers);

    public Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken)
    {
        _drivers.Add(driver);
        return Task.FromResult(Result<Driver>.Success(driver));
    }

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_drivers.SingleOrDefault(d => d.Id == id));

    public Task<Driver?> GetByExternalUserIdAsync(string externalUserId, CancellationToken cancellationToken)
        => Task.FromResult(_drivers.SingleOrDefault(d => d.ExternalUserId == externalUserId));

    // Ordinal ordering, matching the EF repository's column collation. The fake must not
    // define a different notion of "sorted" from the one production uses.
    public Task<IReadOnlyList<Driver>> ListAsync(
        int skip, int take, DriverStatus? status, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Driver>>(
            Filter(status)
                .OrderBy(d => d.LastName, StringComparer.Ordinal)
                .ThenBy(d => d.FirstName, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken)
        => Task.FromResult(Filter(status).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Driver driver, CancellationToken cancellationToken)
    {
        _drivers.Remove(driver);
        return Task.CompletedTask;
    }

    private IEnumerable<Driver> Filter(DriverStatus? status)
        => status is null ? _drivers : _drivers.Where(d => d.Status == status);
}
