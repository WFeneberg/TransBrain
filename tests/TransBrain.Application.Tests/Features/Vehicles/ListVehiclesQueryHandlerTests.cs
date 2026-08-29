using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class ListVehiclesQueryHandlerTests
{
    private static Vehicle VehicleWithPlate(string plate) => Vehicle.Create(
        LicensePlate.Create(plate).Value,
        VehicleType.Van,
        3_000,
        4.0m,
        new DateOnly(2027, 1, 1)).Value;

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository(), cache);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(VehicleWithPlate("M-AA 1"), VehicleWithPlate("M-BB 2"), VehicleWithPlate("M-CC 3"));
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(
            new ListVehiclesQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LicensePlate.Should().Be("M-CC 3");
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FirstPage_ReturnsItemsOrderedByLicensePlate()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(VehicleWithPlate("M-CC 3"), VehicleWithPlate("M-AA 1"));
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        result.Value.Items.Select(i => i.LicensePlate).Should().ContainInOrder("M-AA 1", "M-CC 3");
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingVehiclesAndCountsOnlyThose()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle inWorkshop = VehicleWithPlate("M-WS 1");
        inWorkshop.SendToWorkshop();
        repository.Seed(VehicleWithPlate("M-AV 1"), inWorkshop);
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(new ListVehiclesQuery(Status: "InWorkshop"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Status.Should().Be("InWorkshop");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TypeFilter_ReturnsOnlyMatchingVehicles()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle tractor = Vehicle.Create(
            LicensePlate.Create("M-TR 1").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 1, 1)).Value;
        repository.Seed(VehicleWithPlate("M-VA 1"), tractor);
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(new ListVehiclesQuery(Type: "Tractor"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Type.Should().Be("Tractor");
    }

    [Fact]
    public async Task Handle_StatusAndTypeFilter_AppliesBoth()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle matching = Vehicle.Create(
            LicensePlate.Create("M-MA 1").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 1, 1)).Value;
        matching.SendToWorkshop();
        Vehicle wrongType = VehicleWithPlate("M-WT 1");
        wrongType.SendToWorkshop();
        Vehicle wrongStatus = Vehicle.Create(
            LicensePlate.Create("M-WS 2").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 1, 1)).Value;
        repository.Seed(matching, wrongType, wrongStatus);
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(repository, cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(
                new ListVehiclesQuery(Status: "InWorkshop", Type: "Tractor"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LicensePlate.Should().Be("M-MA 1");
    }

    [Fact]
    public async Task Handle_UnknownStatus_ReturnsValidationError()
    {
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository(), cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(new ListVehiclesQuery(Status: "Sleeping"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownStatus");
    }

    [Fact]
    public async Task Handle_UnknownType_ReturnsValidationError()
    {
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository(), cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(new ListVehiclesQuery(Type: "Rocket"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
    }

    [Fact]
    public async Task Handle_NumericStatus_ReturnsValidationError()
    {
        // Without Enum.IsDefined, "99" would parse to an undefined VehicleStatus and silently
        // filter on it rather than being rejected.
        InMemoryCacheService cache = new();
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository(), cache);

        Result<PagedResult<VehicleResponse>> result =
            await handler.Handle(new ListVehiclesQuery(Status: "99"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownStatus");
    }
}
