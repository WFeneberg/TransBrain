using AwesomeAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Caching;

namespace TransBrain.Api.IntegrationTests;

// RedisCacheService was, until now, the least-proven code on the branch: no suite ran Redis at
// all. TransBrainApiFactory deliberately omits a "cache" connection string so the API's own
// integration tests exercise the in-memory distributed cache instead, and InMemoryCacheService
// (TransBrain.Application.Tests' fake) shares no logic with the real implementation - it is a
// dictionary. So the JSON round-trip of a real PagedResult<T>, Prefix(), the index set, the
// generation counter, and the "connection is null" no-op were all unexercised. These tests run
// RedisCacheService itself against a real, Testcontainers-backed Redis.
public sealed class RedisCacheServiceTests(RedisContainerFixture fixture) : IClassFixture<RedisContainerFixture>
{
    // A fresh, random prefix per test so that sharing one Redis server (and even one logical
    // database) across every test in this class can never let one test's keys, index, or
    // generation counter be seen or mutated by another. Exactly one colon: RedisCacheService's
    // own Prefix() takes everything up to the FIRST colon in a key, so a prefix containing a
    // second colon (e.g. "redistest:<guid>:") would not equal what SetAsync actually indexes
    // under - it would truncate to "redistest:" instead, aliasing every test's entries into one
    // shared index bucket.
    private static string NewPrefix() => $"redistest{Guid.NewGuid():N}:";

    private ICacheService CreateService(IConnectionMultiplexer? connection)
        => new RedisCacheService(CreateDistributedCache(), connection);

    private IDistributedCache CreateDistributedCache()
        => new RedisCache(Options.Create(new RedisCacheOptions { ConnectionMultiplexerFactory = () => Task.FromResult(fixture.Connection) }));

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsAPagedResultOfVehicleResponse()
    {
        ICacheService cache = CreateService(fixture.Connection);
        string prefix = NewPrefix();
        string key = $"{prefix}list:0:1:20:none:none";

        VehicleResponse vehicle = new(
            Guid.CreateVersion7(), "M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31), "Available");
        PagedResult<VehicleResponse> original = new([vehicle], Page: 1, PageSize: 20, TotalCount: 1);

        await cache.SetAsync(key, original, CancellationToken.None);
        PagedResult<VehicleResponse>? roundTripped =
            await cache.GetAsync<PagedResult<VehicleResponse>>(key, CancellationToken.None);

        roundTripped.Should().NotBeNull();
        roundTripped.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesEveryEntryIndexedUnderThatPrefix()
    {
        ICacheService cache = CreateService(fixture.Connection);
        string prefix = NewPrefix();
        string firstKey = $"{prefix}list:0:1:20:none:none";
        string secondKey = $"{prefix}list:0:2:20:none:none";

        await cache.SetAsync(firstKey, new PagedResult<VehicleResponse>([], 1, 20, 0), CancellationToken.None);
        await cache.SetAsync(secondKey, new PagedResult<VehicleResponse>([], 2, 20, 0), CancellationToken.None);

        await cache.RemoveByPrefixAsync(prefix, CancellationToken.None);

        (await cache.GetAsync<PagedResult<VehicleResponse>>(firstKey, CancellationToken.None)).Should().BeNull();
        (await cache.GetAsync<PagedResult<VehicleResponse>>(secondKey, CancellationToken.None)).Should().BeNull();
    }

    // Reproduces the exact race Critical 1 fixed: a reader reads the generation, a concurrent
    // write commits and invalidates, and only then does the reader's own SetAsync run. The
    // resulting entry must be unreachable to anyone computing the key fresh - not deleted (Redis
    // still holds it until its TTL), just orphaned, because nothing will ever ask for that
    // generation again.
    [Fact]
    public async Task GenerationCounter_MakesAPreInvalidationWriteUnreachable()
    {
        ICacheService cache = CreateService(fixture.Connection);
        string prefix = NewPrefix();

        long readerGeneration = await cache.GetGenerationAsync(prefix, CancellationToken.None);
        string readerKey = $"{prefix}list:{readerGeneration}:1:20:none:none";

        // A concurrent write commits and invalidates while the reader above is still between its
        // database read and its own SetAsync call.
        await cache.RemoveByPrefixAsync(prefix, CancellationToken.None);

        // The reader only now reaches SetAsync, using the generation it read before the
        // invalidation.
        PagedResult<VehicleResponse> staleValue = new([], 1, 20, 0);
        await cache.SetAsync(readerKey, staleValue, CancellationToken.None);

        long currentGeneration = await cache.GetGenerationAsync(prefix, CancellationToken.None);
        string freshKey = $"{prefix}list:{currentGeneration}:1:20:none:none";

        currentGeneration.Should().NotBe(readerGeneration);

        // A fresh request recomputes the key against the current generation and gets nothing...
        (await cache.GetAsync<PagedResult<VehicleResponse>>(freshKey, CancellationToken.None)).Should().BeNull();

        // ...even though the stale write did physically land in Redis: it is unreachable, not
        // deleted. This is the "becomes unreachable instead of authoritative" guarantee.
        (await cache.GetAsync<PagedResult<VehicleResponse>>(readerKey, CancellationToken.None))
            .Should().BeEquivalentTo(staleValue);
    }

    [Fact]
    public async Task SetAsync_WithNoMultiplexer_WritesNothing()
    {
        // connection: null is the exact shape Program.cs produces when no "cache" connection
        // string is configured (see RedisCacheService's constructor remarks). SetAsync must be a
        // safe no-op rather than throw, and must not silently write through the IDistributedCache
        // it still holds - the whole point being that caching without RemoveByPrefixAsync's
        // ability to invalidate is a correctness hazard, not a performance win.
        ICacheService cacheWithoutMultiplexer = CreateService(connection: null);
        string prefix = NewPrefix();
        string key = $"{prefix}list:0:1:20:none:none";

        await cacheWithoutMultiplexer.SetAsync(key, new PagedResult<VehicleResponse>([], 1, 20, 0), CancellationToken.None);

        // Verified against a second service instance backed by the same real Redis connection:
        // if SetAsync had written through IDistributedCache despite the null multiplexer, this
        // would find it.
        ICacheService verifier = CreateService(fixture.Connection);
        (await verifier.GetAsync<PagedResult<VehicleResponse>>(key, CancellationToken.None)).Should().BeNull();
    }
}
