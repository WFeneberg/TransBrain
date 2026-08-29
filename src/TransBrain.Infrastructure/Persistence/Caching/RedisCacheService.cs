using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Persistence.Caching;

// IConnectionMultiplexer is nullable, and defaulted to null, because the integration tests run
// with the in-memory distributed cache and no Redis: no connection string means Program.cs never
// registers IConnectionMultiplexer at all. Without the "= null" default, the built-in service
// provider requires every reference-type constructor parameter to resolve to a registered
// service and throws at activation time; a bare nullable annotation is a compile-time-only
// hint that the container does not consult.
internal sealed class RedisCacheService(IDistributedCache cache, IConnectionMultiplexer? connection = null)
    : ICacheService
{
    private static readonly DistributedCacheEntryOptions Options = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        // Caching without the ability to invalidate is a correctness hazard, not a performance
        // win. Without a multiplexer, RemoveByPrefixAsync below has no index to read and no
        // way to find what it wrote, so it can never evict anything: a write would be followed
        // by up to Options' TTL of stale reads. When Redis is absent we would rather stay
        // correct-but-uncached than fast-and-wrong, so writing to the cache is skipped entirely.
        // GetAsync is left as-is: with nothing ever written, it will simply never find a hit.
        if (connection is null)
        {
            return;
        }

        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), Options, cancellationToken);
        await connection.GetDatabase().SetAddAsync(IndexKey(Prefix(key)), key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            return;
        }

        IDatabase database = connection.GetDatabase();
        RedisValue[] keys = await database.SetMembersAsync(IndexKey(prefix));

        if (keys.Length == 0)
        {
            return;
        }

        foreach (RedisValue key in keys)
        {
            await cache.RemoveAsync(key!, cancellationToken);
        }

        // Remove exactly the members just evicted, not the whole index key. A SetAsync that
        // races this call and adds a new member between SetMembersAsync and here must survive
        // the removal, or that key's only index reference would be lost and it could go on
        // serving stale data for up to its own TTL - an unbounded staleness rather than the
        // short window below. This does not close the window entirely: a value written
        // concurrently with this very invalidation can still be briefly stale until the next
        // write invalidates it again.
        await database.SetRemoveAsync(IndexKey(prefix), keys);
    }

    private static string Prefix(string key) => key[..(key.IndexOf(':') + 1)];

    private static string IndexKey(string prefix) => $"__index:{prefix}";
}
