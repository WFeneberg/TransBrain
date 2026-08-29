using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Tours.RemoveOrder;

internal sealed class RemoveOrderCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders)
    : ICommandHandler<RemoveOrderCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        RemoveOrderCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        // Looked up in the tour's own assigned set, not the repository: an order that exists but
        // is not on this tour must answer "not on this tour", not a bare "no such order".
        TransportOrder? order = tour.AssignedOrders
            .SingleOrDefault(o => o.Id == command.TransportOrderId);

        if (order is null)
        {
            return Error.NotFound(
                "Tour.OrderNotAssigned", $"Order '{command.TransportOrderId}' is not on this tour.");
        }

        Result<Unit> removed = tour.Tour.RemoveOrder(order);
        if (!removed.IsSuccess)
        {
            return removed.Error!;
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(
            tour.Tour,
            tour.Vehicle,
            tour.Driver,
            tour.AssignedOrders.Where(o => o.Id != order.Id).ToList());
    }
}
