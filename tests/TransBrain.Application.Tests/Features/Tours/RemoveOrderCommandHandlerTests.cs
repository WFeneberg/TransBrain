using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.RemoveOrder;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Tours;

public class RemoveOrderCommandHandlerTests
{
    private static RemoveOrderCommandHandler Handler(TourFixture f) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders);

    [Fact]
    public async Task Handle_AssignedOrder_DropsItsStopsReturnsItToDraftAndSavesOnce()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new RemoveOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().BeEmpty();
        result.Value.TotalWeightKg.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Draft);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    // Removing the first of two must renumber what is left, so the surviving order's stops stay
    // contiguous from 1 rather than leaving a gap the next assignment would build on.
    [Fact]
    public async Task Handle_FirstOfTwoOrders_LeavesTheSecondRenumberedFromOne()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder first = f.AssignedOrder(sequence: 1);
        TransportOrder second = f.AssignedOrder(sequence: 2);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new RemoveOrderCommand(f.Tour.Id, first.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stops.Should().HaveCount(2);
        result.Value.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2);
        result.Value.Stops.Should().OnlyContain(s => s.TransportOrderId == second.Id);
        first.Status.Should().Be(OrderStatus.Draft);
        second.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public async Task Handle_OrderNotOnTheTour_ReturnsNotFoundAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder elsewhere = TourFixture.AnOrder(sequence: 9);
        f.Orders.Seed(elsewhere);

        Result<TourResponse> result = await Handler(f).Handle(
            new RemoveOrderCommand(f.Tour.Id, elsewhere.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.OrderNotAssigned");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFoundAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new RemoveOrderCommand(Guid.CreateVersion7(), order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotFound");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TourInProgress_ReturnsConflictAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = f.AssignedOrder();
        f.Tour.Start();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new RemoveOrderCommand(f.Tour.Id, order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tour.NotEditable");
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }
}
