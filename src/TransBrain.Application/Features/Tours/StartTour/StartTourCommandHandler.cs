using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.StartTour;

internal sealed class StartTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : ICommandHandler<StartTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        StartTourCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        Result<Unit> allowed = TourAccess.EnsureMayChangeStatus(tour, currentUser);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        // Checked before anything moves. Nothing reaches the database until SaveChangesAsync,
        // so a mid-loop failure could not corrupt storage - but it would leave half the loaded
        // orders mutated in a state that never existed, which the next handler on this scope
        // would then read. One extra pass removes the question entirely.
        TransportOrder? notPlanned = tour.AssignedOrders
            .FirstOrDefault(order => order.Status != OrderStatus.Planned);

        if (notPlanned is not null)
        {
            return Error.Conflict(
                "Tour.OrderNotPlanned",
                $"Order '{notPlanned.OrderNumber.Value}' is '{notPlanned.Status}' and cannot go in transit.");
        }

        Result<Unit> started = tour.Tour.Start();
        if (!started.IsSuccess)
        {
            return started.Error!;
        }

        foreach (TransportOrder order in tour.AssignedOrders)
        {
            order.MarkInTransit();
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
