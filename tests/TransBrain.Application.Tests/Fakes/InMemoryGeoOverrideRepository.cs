using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Geodata;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryGeoOverrideRepository : IGeoOverrideRepository
{
    private readonly List<GeoOverride> _overrides = [];
    private readonly List<UsedAddress> _usedAddresses = [];

    public IReadOnlyList<GeoOverride> Items => _overrides;

    public int SaveChangesCallCount { get; private set; }

    public InMemoryGeoOverrideRepository Seed(GeoOverride geoOverride)
    {
        _overrides.Add(geoOverride);
        return this;
    }

    public InMemoryGeoOverrideRepository SeedUsedAddress(UsedAddress address)
    {
        _usedAddresses.Add(address);
        return this;
    }

    public Task<GeoOverride?> GetByKeyAsync(string addressKey, CancellationToken cancellationToken)
        => Task.FromResult(_overrides.SingleOrDefault(o => o.AddressKey == addressKey));

    public Task<GeoOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_overrides.SingleOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<GeoOverride>> ListAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GeoOverride>>(_overrides.ToList());

    public Task AddAsync(GeoOverride geoOverride, CancellationToken cancellationToken)
    {
        _overrides.Add(geoOverride);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(GeoOverride geoOverride, CancellationToken cancellationToken)
    {
        _overrides.Remove(geoOverride);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UsedAddress>> ListUsedAddressesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<UsedAddress>>(_usedAddresses.ToList());
}
