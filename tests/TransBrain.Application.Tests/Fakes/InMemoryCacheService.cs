using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _entries = [];

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

        foreach (string key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _entries.Remove(key);
        }

        return Task.CompletedTask;
    }
}
