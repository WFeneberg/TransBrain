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
        ListVehiclesQueryHandler handler = new(new InMemoryVehicleRepository());

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
        ListVehiclesQueryHandler handler = new(repository);

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
        ListVehiclesQueryHandler handler = new(repository);

        Result<PagedResult<VehicleResponse>> result = await handler.Handle(new ListVehiclesQuery(), CancellationToken.None);

        result.Value.Items.Select(i => i.LicensePlate).Should().ContainInOrder("M-AA 1", "M-CC 3");
    }
}
