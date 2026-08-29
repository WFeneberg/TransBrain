using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.AssignOrder;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Tours;

public class AssignOrderCommandHandlerTests
{
    private static AssignOrderCommandHandler Handler(TourFixture f) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders);

    [Fact]
    public async Task Handle_DraftOrder_AddsTwoStopsPlansTheOrderAndSavesOnce()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = TourFixture.AnOrder();
        f.Orders.Seed(order);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops[0].StopType.Should().Be("Pickup");
        result.Value.Stops[0].OrderNumber.Should().Be(order.OrderNumber.Value);
        result.Value.Stops[1].StopType.Should().Be("Delivery");
        result.Value.TotalWeightKg.Should().Be(5_000);
        order.Status.Should().Be(OrderStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFoundAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = TourFixture.AnOrder();
        f.Orders.Seed(order);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(Guid.CreateVersion7(), order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFoundAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.NotFound");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OrderExceedingPayload_ReturnsConflictAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create(payloadKg: 6_000);
        TransportOrder order = TourFixture.AnOrder(weightKg: 7_000);
        f.Orders.Seed(order);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.PayloadExceeded");
        order.Status.Should().Be(OrderStatus.Draft);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    // The capacity sum must count what the tour already carries, not just the incoming order. A
    // handler that passed an empty list to the domain would pass every other test in this file
    // and still let a dispatcher overload a lorry one order at a time.
    [Fact]
    public async Task Handle_SecondOrderTippingItOverThePayload_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create(payloadKg: 10_000);
        TransportOrder first = TourFixture.AnOrder(weightKg: 6_000, sequence: 1);
        TransportOrder second = TourFixture.AnOrder(weightKg: 5_000, sequence: 2);
        f.Orders.Seed(first, second);
        await Handler(f).Handle(new AssignOrderCommand(f.Tour.Id, first.Id), CancellationToken.None);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.PayloadExceeded");
        second.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Handle_SecondOrderTippingItOverTheLoadMeters_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create(loadMeters: 8.0m);
        TransportOrder first = TourFixture.AnOrder(loadMeters: 5.0m, sequence: 1);
        TransportOrder second = TourFixture.AnOrder(loadMeters: 3.5m, sequence: 2);
        f.Orders.Seed(first, second);
        await Handler(f).Handle(new AssignOrderCommand(f.Tour.Id, first.Id), CancellationToken.None);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LoadMetersExceeded");
    }

    [Fact]
    public async Task Handle_OrderAlreadyPlanned_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = TourFixture.AnOrder();
        order.MarkPlanned();
        f.Orders.Seed(order);

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TourAlreadyInProgress_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder(sequence: 1);
        f.Tour.Start();
        TransportOrder late = TourFixture.AnOrder(sequence: 2);
        f.Orders.Seed(late);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new AssignOrderCommand(f.Tour.Id, late.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }
}
