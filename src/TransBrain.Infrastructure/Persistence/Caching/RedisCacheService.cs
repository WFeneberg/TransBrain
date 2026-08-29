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
// hint that the container does not consult. With the default in place, entries still cache and
// expire on their own when Redis is absent; only prefix invalidation degrades, and the tests
// that care about invalidation use the in-memory fake instead of this type.
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
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), Options, cancellationToken);

        if (connection is not null)
        {
            await connection.GetDatabase().SetAddAsync(IndexKey(Prefix(key)), key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            return;
        }

        IDatabase database = connection.GetDatabase();
        RedisValue[] keys = await database.SetMembersAsync(IndexKey(prefix));

        foreach (RedisValue key in keys)
        {
            await cache.RemoveAsync(key!, cancellationToken);
        }

        await database.KeyDeleteAsync(IndexKey(prefix));
    }

    private static string Prefix(string key) => key[..(key.IndexOf(':') + 1)];

    private static string IndexKey(string prefix) => $"__index:{prefix}";
}
