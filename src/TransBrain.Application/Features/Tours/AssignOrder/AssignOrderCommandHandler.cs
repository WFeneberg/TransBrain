using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.AssignOrder;

internal sealed class AssignOrderCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders)
    : ICommandHandler<AssignOrderCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        AssignOrderCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        TransportOrder? order = await orders.GetByIdAsync(command.TransportOrderId, cancellationToken);
        if (order is null)
        {
            return Error.NotFound(
                "TransportOrder.NotFound", $"No transport order with id '{command.TransportOrderId}'.");
        }

        // AssignedOrders is what makes the capacity sum count the whole tour rather than just
        // this one order - the difference between a full lorry and an overloaded one.
        Result<Unit> assigned = tour.Tour.AssignOrder(order, tour.Vehicle, tour.AssignedOrders);
        if (!assigned.IsSuccess)
        {
            return assigned.Error!;
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, [.. tour.AssignedOrders, order]);
    }
}
