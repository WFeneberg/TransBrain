using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.StartTour;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Tests.Features.Tours;

public class StartTourCommandHandlerTests
{
    private static StartTourCommandHandler Handler(TourFixture f, StubCurrentUser? caller = null) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders, caller ?? StubCurrentUser.Dispatcher());

    [Fact]
    public async Task Handle_PlannedTourAsDispatcher_StartsItAndMovesEveryOrderToInTransit()
    {
        TourFixture f = TourFixture.Create();
        TransportOrder first = f.AssignedOrder(sequence: 1);
        TransportOrder second = f.AssignedOrder(sequence: 2);
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f).Handle(
            new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("InProgress");
        first.Status.Should().Be(OrderStatus.InTransit);
        second.Status.Should().Be(OrderStatus.InTransit);
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TourWithoutStops_ReturnsConflictAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f).Handle(
            new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NoStops");
        f.Tour.Status.Should().Be(TourStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AlreadyInProgress_ReturnsConflict()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();
        await Handler(f).Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

        Result<TourResponse> result = await Handler(f).Handle(
            new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        // Fails on the order precondition first: the assigned orders are already InTransit, so
        // that is the honest reason, and it is reported before the tour's own transition guard.
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_UnknownTour_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f).Handle(
            new StartTourCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
    }

    [Fact]
    public async Task Handle_AsTheAssignedDriver_Succeeds()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("driver-sub"))
            .Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("InProgress");
        f.Tours.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AsADifferentDriver_ReturnsForbiddenAndDoesNotSave()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        TransportOrder order = f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("someone-else"))
            .Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Tour.NotYours");
        f.Tour.Status.Should().Be(TourStatus.Planned);
        order.Status.Should().Be(OrderStatus.Planned);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    // A driver record with no ExternalUserId belongs to nobody who can sign in. Treating that
    // missing link as "matches everyone" would hand such a tour to whichever driver asked first.
    [Fact]
    public async Task Handle_AsADriverWhenTheTourDriverHasNoExternalUserId_ReturnsForbidden()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: null);
        f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("driver-sub"))
            .Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        f.Tours.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AsAdmin_Succeeds()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");
        f.AssignedOrder();
        f.Tours.ResetSaveCount();

        Result<TourResponse> result = await Handler(f, StubCurrentUser.Admin())
            .Handle(new StartTourCommand(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
