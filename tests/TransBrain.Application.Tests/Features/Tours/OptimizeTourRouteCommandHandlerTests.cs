using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.OptimizeTourRoute;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Tours;

public class OptimizeTourRouteCommandHandlerTests
{
    private static FakeGeocoder Geocoder() =>
        new FakeGeocoder()
            .Knows("München", 48.137, 11.575)
            .Knows("Augsburg", 48.371, 10.898)
            .Knows("Berlin", 52.520, 13.405)
            .Knows("Potsdam", 52.391, 13.064);

    private static OptimizeTourRouteCommandHandler Handler(TourFixture f, FakeGeocoder geocoder) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders,
            new InMemoryGeoOverrideRepository(), geocoder, new InMemoryCacheService());

    /// <summary>An order from one known city to another, assigned to the fixture's tour.</summary>
    private static TransportOrder AssignedOrderBetween(
        TourFixture f, string fromCity, string toCity, int sequence)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            Address.Create($"Absender {fromCity}", "Hauptstr. 1", "80331", fromCity, "DE").Value,
            Address.Create($"Empfaenger {toCity}", "Bahnhofstr. 2", "10115", toCity, "DE").Value,
            Cargo.Create("Palettenware", 1_000, 1.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;

        f.Orders.Seed(order);
        List<TransportOrder> alreadyAssigned = f.Tour.AssignedOrderIds()
            .Select(id => f.Orders.Orders.Single(o => o.Id == id))
            .ToList();
        f.Tour.AssignOrder(order, f.Vehicle, alreadyAssigned);
        return order;
    }

    [Fact]
    public async Task Handle_ZigZagStopOrder_ReordersStopsAndSaves()
    {
        TourFixture f = TourFixture.Create();
        // As assigned: München -> Berlin -> Augsburg -> Potsdam, twice across the country.
        TransportOrder south = AssignedOrderBetween(f, "München", "Berlin", 1);
        TransportOrder north = AssignedOrderBetween(f, "Augsburg", "Potsdam", 2);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, Geocoder()).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Both southern pickups first, then the two northern deliveries, nearest first:
        // München -> Augsburg -> Potsdam -> Berlin instead of twice across the country.
        result.Value.Stops.Select(stop => (stop.OrderNumber, stop.StopType)).Should().Equal(
            (south.OrderNumber.Value, "Pickup"),
            (north.OrderNumber.Value, "Pickup"),
            (north.OrderNumber.Value, "Delivery"),
            (south.OrderNumber.Value, "Delivery"));
        result.Value.Stops.Select(stop => stop.Sequence).Should().Equal(1, 2, 3, 4);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AlreadyOptimalOrder_AnswersTheTourWithoutSaving()
    {
        TourFixture f = TourFixture.Create();
        // München -> Augsburg -> Potsdam -> Berlin is already the shortest valid order.
        AssignedOrderBetween(f, "München", "Augsburg", 1);
        AssignedOrderBetween(f, "Potsdam", "Berlin", 2);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, Geocoder()).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Select(stop => stop.Sequence).Should().Equal(1, 2, 3, 4);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SingleOrder_AnswersTheTourUnchanged()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, Geocoder()).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().HaveCount(2);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TourWithoutStops_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f, Geocoder()).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NoStops");
    }

    [Fact]
    public async Task Handle_TourAlreadyStarted_ReturnsConflictWithoutGeocoding()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        f.Tour.Start();
        // Knows nothing: reaching the geocoder would fail the request with a different error.
        FakeGeocoder geocoder = new();

        Result<TourResponse> result = await Handler(f, geocoder).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }

    [Fact]
    public async Task Handle_UnlocatableStop_ReturnsTheGeocodingErrorAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        AssignedOrderBetween(f, "München", "Berlin", 1);
        AssignedOrderBetween(f, "Augsburg", "Potsdam", 2);
        f.Tours.ResetSaveCount();
        FakeGeocoder geocoder = new FakeGeocoder().Knows("München", 48.137, 11.575);

        Result<TourResponse> result = await Handler(f, geocoder).Handle(
            new OptimizeTourRouteCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Geocoding.NoMatch");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f, Geocoder()).Handle(
            new OptimizeTourRouteCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
    }
}
