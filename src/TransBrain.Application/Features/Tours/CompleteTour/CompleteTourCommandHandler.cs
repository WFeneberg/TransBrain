using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.CompleteTour;

internal sealed class CompleteTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : ICommandHandler<CompleteTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        CompleteTourCommand command,
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

        // Same two-pass reasoning as StartTourCommandHandler.
        TransportOrder? notInTransit = tour.AssignedOrders
            .FirstOrDefault(order => order.Status != OrderStatus.InTransit);

        if (notInTransit is not null)
        {
            return Error.Conflict(
                "Tour.OrderNotInTransit",
                $"Order '{notInTransit.OrderNumber.Value}' is '{notInTransit.Status}' and cannot be delivered.");
        }

        Result<Unit> completed = tour.Tour.Complete();
        if (!completed.IsSuccess)
        {
            return completed.Error!;
        }

        foreach (TransportOrder order in tour.AssignedOrders)
        {
            order.MarkDelivered();
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
