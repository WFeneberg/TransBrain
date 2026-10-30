using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.GetTourRoute;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Features.Tours;

public class GetTourRouteQueryHandlerTests
{
    private static readonly DateTimeOffset Departure = new(2027, 3, 1, 5, 0, 0, TimeSpan.Zero);

    private static FakeGeocoder Geocoder() =>
        new FakeGeocoder()
            .Knows("München", 48.137, 11.575)
            .Knows("Berlin", 52.520, 13.405);

    private static GetTourRouteQueryHandler Handler(
        TourFixture f,
        FakeGeocoder geocoder,
        FakeRoutePlanner planner,
        Fakes.StubCurrentUser? caller = null) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders, caller ?? Fakes.StubCurrentUser.Dispatcher(),
            geocoder, planner, new InMemoryCacheService());

    [Fact]
    public async Task Handle_TwoStopTour_ReturnsRouteStopsAndSchedule()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();

        Result<TourRouteResponse> result = await Handler(f, Geocoder(), new FakeRoutePlanner())
            .Handle(new GetTourRouteQuery(f.Tour.Id, Departure), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Select(stop => stop.StopType).Should().Equal("Pickup", "Delivery");
        result.Value.Stops[0].Place.Should().Be("Absender GmbH, München");
        result.Value.DistanceMeters.Should().Be(100_000);
        result.Value.DrivingSeconds.Should().Be(TimeSpan.FromHours(2).TotalSeconds);
        result.Value.Schedule.Should().Contain(entry => entry.Kind == "Driving");
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnlocatableStop_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        FakeGeocoder geocoder = new FakeGeocoder().Knows("München", 48.137, 11.575);

        Result<TourRouteResponse> result = await Handler(f, geocoder, new FakeRoutePlanner())
            .Handle(new GetTourRouteQuery(f.Tour.Id, Departure), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Geocoding.NoMatch");
    }

    [Fact]
    public async Task Handle_RoutingUnavailable_ReturnsUnavailable()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        FakeRoutePlanner planner = new() { Failure = Error.Unavailable("Routing.NotConfigured", "Not set.") };

        Result<TourRouteResponse> result = await Handler(f, Geocoder(), planner)
            .Handle(new GetTourRouteQuery(f.Tour.Id, Departure), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task Handle_TourWithoutStops_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();

        Result<TourRouteResponse> result = await Handler(f, Geocoder(), new FakeRoutePlanner())
            .Handle(new GetTourRouteQuery(f.Tour.Id, Departure), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NoStops");
    }

    [Fact]
    public async Task Handle_AsADifferentDriver_ReturnsForbidden()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        f.AssignedOrder();

        Result<TourRouteResponse> result = await Handler(
                f, Geocoder(), new FakeRoutePlanner(), Fakes.StubCurrentUser.DriverWith("someone-else"))
            .Handle(new GetTourRouteQuery(f.Tour.Id, Departure), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }
}
