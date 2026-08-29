using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Features.Drivers.ListDrivers;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class ListDriversCachingTests
{
    private static Driver DriverNamed(string firstName, string lastName) =>
        Driver.Create(firstName, lastName, [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;

    [Fact]
    public async Task Handle_CalledTwice_HitsTheRepositoryOnlyOnce()
    {
        CountingDriverRepository repository = new();
        InMemoryCacheService cache = new();
        ListDriversQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListDriversQuery(), CancellationToken.None);
        await handler.Handle(new ListDriversQuery(), CancellationToken.None);

        repository.ListCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentPage_DoesNotServeTheFirstPagesCachedResult()
    {
        CountingDriverRepository repository = new();
        InMemoryCacheService cache = new();
        ListDriversQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListDriversQuery(Page: 1), CancellationToken.None);
        await handler.Handle(new ListDriversQuery(Page: 2), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DifferentStatusFilter_DoesNotServeTheOtherFiltersCachedResult()
    {
        CountingDriverRepository repository = new();
        InMemoryCacheService cache = new();
        ListDriversQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListDriversQuery(Status: "Available"), CancellationToken.None);
        await handler.Handle(new ListDriversQuery(Status: "Absent"), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateDriver_AfterAListWasCached_InvalidatesIt()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        ListDriversQueryHandler list = new(repository, cache);
        CreateDriverCommandHandler create = new(repository, cache);

        await list.Handle(new ListDriversQuery(), CancellationToken.None);
        await create.Handle(
            new CreateDriverCommand("New", "Driver", ["C"], new DateOnly(2028, 1, 1), null),
            CancellationToken.None);

        Result<PagedResult<DriverResponse>> after = await list.Handle(new ListDriversQuery(), CancellationToken.None);

        after.Value.Items.Should().ContainSingle();
        cache.RemoveByPrefixCallCount.Should().Be(1);
    }

    // Regression test for the read-then-set race: a list handler that already read the
    // generation and is about to write must not resurrect a page a concurrent write just
    // invalidated. Simulated directly against the fake, since it implements the same
    // generation-counter contract as RedisCacheServiceTests exercises against real Redis.
    [Fact]
    public async Task Handle_GenerationBumpedBetweenReadAndWrite_TheStaleWriteIsNeverServed()
    {
        InMemoryCacheService cache = new();
        long staleGeneration = await cache.GetGenerationAsync(DriverCacheKeys.Prefix, CancellationToken.None);
        string staleKey = $"{DriverCacheKeys.Prefix}list:{staleGeneration}:1:20:none";

        // A concurrent write commits and invalidates while our "reader" is still mid-flight.
        await cache.RemoveByPrefixAsync(DriverCacheKeys.Prefix, CancellationToken.None);

        // Our reader only now reaches its own SetAsync, still using the generation it read
        // before the invalidation above.
        PagedResult<DriverResponse> stale = new([], 1, 20, 0);
        await cache.SetAsync(staleKey, stale, CancellationToken.None);

        // A fresh request recomputes the key against the current generation and must not see it.
        long currentGeneration = await cache.GetGenerationAsync(DriverCacheKeys.Prefix, CancellationToken.None);
        string currentKey = $"{DriverCacheKeys.Prefix}list:{currentGeneration}:1:20:none";

        currentGeneration.Should().NotBe(staleGeneration);
        (await cache.GetAsync<PagedResult<DriverResponse>>(currentKey, CancellationToken.None)).Should().BeNull();
    }
}
