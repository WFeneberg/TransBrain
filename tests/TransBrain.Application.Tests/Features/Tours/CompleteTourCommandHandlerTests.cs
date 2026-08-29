using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.CompleteTour;
using TransBrain.Application.Features.Tours.StartTour;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Tests.Features.Tours;

public class CompleteTourCommandHandlerTests
{
    private static CompleteTourCommandHandler Handler(TourFixture f, StubCurrentUser? caller = null) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders, caller ?? StubCurrentUser.Dispatcher());

    private static Task StartAsync(TourFixture f) =>
        new StartTourCommandHandler(f.Tours, f.Vehicles, f.Drivers, f.Orders, StubCurrentUser.Dispatcher())
            .Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

    [Fact]
    public async Task Handle_InProgressTourAsDispatcher_CompletesItAndDeliversEveryOrder()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder first = f.AssignedOrder(sequence: 1);
        TransportOrder second = f.AssignedOrder(sequence: 2);
        await StartAsync(f);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
        first.Status.Should().Be(OrderStatus.Delivered);
        second.Status.Should().Be(OrderStatus.Delivered);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PlannedTour_ReturnsConflictAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder order = f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        // The order is still Planned, which is the reason reported - the tour never started.
        result.Error.Code.Should().Be("Tour.OrderNotInTransit");
        f.Tour.Status.Should().Be(TourStatus.Planned);
        order.Status.Should().Be(OrderStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        await StartAsync(f);
        await Handler(f).Handle(new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        Result<TourResponse> result = await Handler(f).Handle(
            new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        f.Tour.Status.Should().Be(TourStatus.Completed);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f).Handle(
            new CompleteTourCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotFound");
    }

    [Fact]
    public async Task Handle_AsTheAssignedDriver_Succeeds()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        f.AssignedOrder();
        await StartAsync(f);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("driver-sub"))
            .Handle(new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Handle_AsADifferentDriver_ReturnsForbiddenAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        TransportOrder order = f.AssignedOrder();
        await StartAsync(f);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("someone-else"))
            .Handle(new CompleteTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Tour.NotYours");
        f.Tour.Status.Should().Be(TourStatus.InProgress);
        order.Status.Should().Be(OrderStatus.InTransit);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }
}
