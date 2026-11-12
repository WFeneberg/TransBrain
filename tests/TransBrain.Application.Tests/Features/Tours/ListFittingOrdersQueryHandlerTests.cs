using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.ListFittingOrders;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Tours;

public class ListFittingOrdersQueryHandlerTests
{
    private static ListFittingOrdersQueryHandler Handler(TourFixture f) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders);

    [Fact]
    public async Task Handle_DraftOrdersWithinRemainingCapacity_AnswersOnlyThoseAndTheHeadroom()
    {
        TourFixture f = TourFixture.Create(payloadKg: 10_000, loadMeters: 10.0m);
        f.AssignedOrder(weightKg: 6_000, loadMeters: 4.0m, sequence: 1);
        TransportOrder fits = TourFixture.AnOrder(weightKg: 4_000, loadMeters: 6.0m, sequence: 2);
        TransportOrder tooHeavy = TourFixture.AnOrder(weightKg: 4_001, loadMeters: 1.0m, sequence: 3);
        TransportOrder tooLong = TourFixture.AnOrder(weightKg: 1_000, loadMeters: 6.1m, sequence: 4);
        f.Orders.Seed(fits, tooHeavy, tooLong);

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RemainingWeightKg.Should().Be(4_000);
        result.Value.RemainingLoadMeters.Should().Be(6.0m);
        result.Value.Orders.Should().ContainSingle()
            .Which.OrderNumber.Should().Be(fits.OrderNumber.Value);
    }

    [Fact]
    public async Task Handle_PlannedOrderThatWouldFit_IsNotOffered()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder planned = TourFixture.AnOrder(weightKg: 1_000);
        planned.MarkPlanned();
        f.Orders.Seed(planned);

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrderFillingTheRemainingCapacityExactly_IsOffered()
    {
        TourFixture f = TourFixture.Create(payloadKg: 10_000, loadMeters: 10.0m);
        f.AssignedOrder(weightKg: 6_000, loadMeters: 4.0m, sequence: 1);
        TransportOrder exact = TourFixture.AnOrder(weightKg: 4_000, loadMeters: 6.0m, sequence: 2);
        f.Orders.Seed(exact);

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_TourAlreadyStarted_AnswersNoOrdersButStillTheHeadroom()
    {
        TourFixture f = TourFixture.Create(payloadKg: 10_000);
        f.AssignedOrder(weightKg: 6_000, sequence: 1);
        f.Tour.Start();
        f.Orders.Seed(TourFixture.AnOrder(weightKg: 1_000, sequence: 2));

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Orders.Should().BeEmpty();
        result.Value.RemainingWeightKg.Should().Be(4_000);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
    }

    [Fact]
    public async Task Handle_FittingOrders_AreSortedByPickupStart()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder later = OrderPickedUpAt(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero), 2);
        TransportOrder earlier = OrderPickedUpAt(new DateTimeOffset(2027, 3, 1, 6, 0, 0, TimeSpan.Zero), 3);
        f.Orders.Seed(later, earlier);

        Result<FittingOrdersResponse> result = await Handler(f).Handle(
            new ListFittingOrdersQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Orders.Select(order => order.OrderNumber).Should().Equal(
            earlier.OrderNumber.Value, later.OrderNumber.Value);
    }

    private static TransportOrder OrderPickedUpAt(DateTimeOffset pickup, int sequence)
    {
        Address consignor = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        Address consignee = Address.Create("Empfaenger AG", "Bahnhofstr. 2", "10115", "Berlin", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            consignor,
            consignee,
            Cargo.Create("Palettenware", 1_000, 1.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
    }
}
