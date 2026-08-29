using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _entries = [];
    private readonly Dictionary<string, long> _generations = [];

    public int RemoveByPrefixCallCount { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => Task.FromResult(_entries.TryGetValue(key, out object? value) ? (T)value : null);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        _entries[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        RemoveByPrefixCallCount++;

        // Bumped unconditionally, mirroring RedisCacheService: the counter is what closes the
        // read-then-set race, entry deletion below is only cleanup.
        _generations[prefix] = _generations.GetValueOrDefault(prefix) + 1;

        foreach (string key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task<long> GetGenerationAsync(string prefix, CancellationToken cancellationToken)
        => Task.FromResult(_generations.GetValueOrDefault(prefix));
}
