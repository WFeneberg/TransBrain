using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class ListVehiclesCachingTests
{
    [Fact]
    public async Task Handle_CalledTwice_HitsTheRepositoryOnlyOnce()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        repository.ListCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentPage_DoesNotServeTheFirstPagesCachedResult()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(Page: 1), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(Page: 2), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DifferentStatusFilter_DoesNotServeTheOtherFiltersCachedResult()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(Status: "Available"), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(Status: "InWorkshop"), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DifferentTypeFilter_DoesNotServeTheOtherFiltersCachedResult()
    {
        CountingVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        await handler.Handle(new ListVehiclesQuery(Type: "Van"), CancellationToken.None);
        await handler.Handle(new ListVehiclesQuery(Type: "Tractor"), CancellationToken.None);

        repository.ListCallCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateVehicle_AfterAListWasCached_InvalidatesIt()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler list = new(repository, cache);
        CreateVehicleCommandHandler create = new(repository, cache);

        await list.Handle(new ListVehiclesQuery(), CancellationToken.None);
        await create.Handle(
            new CreateVehicleCommand("M-NEW 1", "Van", 3_000, 4.0m, new DateOnly(2028, 1, 1)),
            CancellationToken.None);

        Result<PagedResult<VehicleResponse>> after = await list.Handle(new ListVehiclesQuery(), CancellationToken.None);

        after.Value.Items.Should().ContainSingle();
        cache.RemoveByPrefixCallCount.Should().Be(1);
    }
}
