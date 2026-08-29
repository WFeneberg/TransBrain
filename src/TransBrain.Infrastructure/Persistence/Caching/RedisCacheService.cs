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

        // Bumped first, and unconditionally - even when there is nothing to delete below. A
        // reader that already read the pre-bump generation and is mid-flight (past its own
        // GetAsync miss, not yet at SetAsync) will finish writing under that stale generation
        // once this returns. Nothing will ever compute that generation again, so the write is
        // orphaned rather than served: see GetGenerationAsync's remarks on ICacheService for the
        // full race this closes. Deleting entries below is still worth doing so stale-generation
        // values do not linger in Redis for their full TTL, but the deletion alone was never
        // enough to close the race - only the counter is.
        await database.StringIncrementAsync(GenerationKey(prefix));

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

    public async Task<long> GetGenerationAsync(string prefix, CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            return 0;
        }

        RedisValue value = await connection.GetDatabase().StringGetAsync(GenerationKey(prefix));
        return value.IsNullOrEmpty ? 0 : (long)value;
    }

    // Guarded against a key with no ':' at all: key.IndexOf(':') would then be -1, and
    // key[..(-1 + 1)] is key[..0] - the empty string. Every such key would share one "" prefix,
    // land in the same __index: bucket as every other colon-less key, and never be reachable by
    // RemoveByPrefixAsync (which is always called with a real, non-empty prefix ending in ':').
    // Falling back to the whole key keeps it out of that shared, permanently-unevictable bucket.
    private static string Prefix(string key)
    {
        int separator = key.IndexOf(':');
        return separator < 0 ? key : key[..(separator + 1)];
    }

    private static string IndexKey(string prefix) => $"__index:{prefix}";

    private static string GenerationKey(string prefix) => $"__gen:{prefix}";
}
