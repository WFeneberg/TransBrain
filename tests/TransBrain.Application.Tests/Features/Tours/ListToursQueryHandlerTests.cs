using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.ListTours;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Tours;

public class ListToursQueryHandlerTests
{
    private static readonly DateOnly March1 = new(2027, 3, 1);

    private sealed record Scene(
        InMemoryTourRepository Tours,
        InMemoryVehicleRepository Vehicles,
        InMemoryDriverRepository Drivers,
        InMemoryTransportOrderRepository Orders)
    {
        public ListToursQueryHandler Handler(StubCurrentUser? caller = null) =>
            new(Tours, Vehicles, Drivers, Orders, caller ?? StubCurrentUser.Dispatcher());
    }

    private static Scene EmptyScene() => new(new(), new(), new(), new());

    private static Vehicle AddVehicle(Scene scene, string plate)
    {
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create(plate).Value, VehicleType.RigidTruck, 18_000, 13.6m,
            new DateOnly(2028, 1, 1)).Value;
        scene.Vehicles.Seed(vehicle);
        return vehicle;
    }

    private static Driver AddDriver(Scene scene, string lastName, string? externalUserId = null)
    {
        Driver driver = Driver.Create(
            "Frank", lastName, [LicenseClass.CE], new DateOnly(2028, 6, 30), externalUserId).Value;
        scene.Drivers.Seed(driver);
        return driver;
    }

    private static Tour AddTour(Scene scene, DateOnly date, Vehicle vehicle, Driver driver)
    {
        Tour tour = Tour.Create(date, vehicle, driver).Value;
        scene.Tours.Seed(tour);
        return tour;
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        Scene scene = EmptyScene();

        Result<PagedResult<TourResponse>> result =
            await scene.Handler().Handle(new ListToursQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FirstPage_OrdersByTourDate()
    {
        Scene scene = EmptyScene();
        Driver driver = AddDriver(scene, "Meier");
        AddTour(scene, March1.AddDays(2), AddVehicle(scene, "M-AA 1003"), driver);
        AddTour(scene, March1, AddVehicle(scene, "M-AA 1001"), driver);
        AddTour(scene, March1.AddDays(1), AddVehicle(scene, "M-AA 1002"), driver);

        Result<PagedResult<TourResponse>> result =
            await scene.Handler().Handle(new ListToursQuery(), CancellationToken.None);

        result.Value.Items.Select(t => t.TourDate)
            .Should().ContainInOrder(March1, March1.AddDays(1), March1.AddDays(2));
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        Scene scene = EmptyScene();
        Driver driver = AddDriver(scene, "Meier");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 2001"), driver);
        AddTour(scene, March1.AddDays(1), AddVehicle(scene, "M-AA 2002"), driver);
        AddTour(scene, March1.AddDays(2), AddVehicle(scene, "M-AA 2003"), driver);

        Result<PagedResult<TourResponse>> result = await scene.Handler()
            .Handle(new ListToursQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle()
            .Which.TourDate.Should().Be(March1.AddDays(2));
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_TourDateFilter_ReturnsOnlyThatDay()
    {
        Scene scene = EmptyScene();
        Driver driver = AddDriver(scene, "Meier");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 3001"), driver);
        AddTour(scene, March1.AddDays(1), AddVehicle(scene, "M-AA 3002"), driver);

        Result<PagedResult<TourResponse>> result = await scene.Handler()
            .Handle(new ListToursQuery(TourDate: March1), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.TourDate.Should().Be(March1);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_VehicleFilter_ReturnsOnlyThatVehiclesTours()
    {
        Scene scene = EmptyScene();
        Driver driver = AddDriver(scene, "Meier");
        Vehicle wanted = AddVehicle(scene, "M-AA 4001");
        AddTour(scene, March1, wanted, driver);
        AddTour(scene, March1.AddDays(1), AddVehicle(scene, "M-AA 4002"), driver);

        Result<PagedResult<TourResponse>> result = await scene.Handler()
            .Handle(new ListToursQuery(VehicleId: wanted.Id), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.VehicleId.Should().Be(wanted.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DriverFilter_ReturnsOnlyThatDriversTours()
    {
        Scene scene = EmptyScene();
        Driver wanted = AddDriver(scene, "Gesucht");
        Driver other = AddDriver(scene, "Andere");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 5001"), wanted);
        AddTour(scene, March1, AddVehicle(scene, "M-AA 5002"), other);

        Result<PagedResult<TourResponse>> result = await scene.Handler()
            .Handle(new ListToursQuery(DriverId: wanted.Id), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.DriverId.Should().Be(wanted.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AsADriver_NarrowsToTheirOwnToursEvenWithoutAFilter()
    {
        Scene scene = EmptyScene();
        Driver mine = AddDriver(scene, "Meins", "driver-sub");
        Driver other = AddDriver(scene, "Fremd", "other-sub");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 6001"), mine);
        AddTour(scene, March1, AddVehicle(scene, "M-AA 6002"), other);

        Result<PagedResult<TourResponse>> result =
            await scene.Handler(StubCurrentUser.DriverWith("driver-sub"))
                .Handle(new ListToursQuery(), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.DriverId.Should().Be(mine.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    // A driver who edits the query string must not be able to widen their own scope.
    [Fact]
    public async Task Handle_AsADriverAskingForSomeoneElsesDriverId_StillOnlySeesTheirOwn()
    {
        Scene scene = EmptyScene();
        Driver mine = AddDriver(scene, "Meins", "driver-sub");
        Driver other = AddDriver(scene, "Fremd", "other-sub");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 7001"), mine);
        AddTour(scene, March1, AddVehicle(scene, "M-AA 7002"), other);

        Result<PagedResult<TourResponse>> result =
            await scene.Handler(StubCurrentUser.DriverWith("driver-sub"))
                .Handle(new ListToursQuery(DriverId: other.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    // A signed-in fahrer whose login is bound to no driver record sees nothing, rather than
    // falling through to an unfiltered list.
    [Fact]
    public async Task Handle_AsADriverWithNoMatchingDriverRecord_ReturnsEmptyPage()
    {
        Scene scene = EmptyScene();
        Driver someone = AddDriver(scene, "Jemand", "other-sub");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 8001"), someone);

        Result<PagedResult<TourResponse>> result =
            await scene.Handler(StubCurrentUser.DriverWith("unbound-sub"))
                .Handle(new ListToursQuery(), CancellationToken.None);

        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    // Spec §9 narrows the fahrer row and only that row: a viewer reads everything. An earlier
    // version of the handler scoped "anyone who is not admin or disponent", which silently gave
    // every viewer an empty list.
    [Fact]
    public async Task Handle_AsViewer_SeesEveryTour()
    {
        Scene scene = EmptyScene();
        Driver one = AddDriver(scene, "Eins", "sub-one");
        Driver two = AddDriver(scene, "Zwei", "sub-two");
        AddTour(scene, March1, AddVehicle(scene, "M-AA 9001"), one);
        AddTour(scene, March1, AddVehicle(scene, "M-AA 9002"), two);

        Result<PagedResult<TourResponse>> result = await scene.Handler(StubCurrentUser.Viewer())
            .Handle(new ListToursQuery(), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }
}
