using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Orders;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours.ListFittingOrders;

internal sealed class ListFittingOrdersQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders)
    : IQueryHandler<ListFittingOrdersQuery, FittingOrdersResponse>
{
    public async Task<Result<FittingOrdersResponse>> Handle(
        ListFittingOrdersQuery query,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            query.TourId, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        int remainingWeightKg = tour.Vehicle.PayloadKg
            - tour.AssignedOrders.Sum(order => order.Cargo.WeightKg);
        decimal remainingLoadMeters = tour.Vehicle.LoadMeters
            - tour.AssignedOrders.Sum(order => order.Cargo.LoadMeters);

        // Only a planned tour can still take orders, so for any other status the honest answer
        // is "nothing fits" - not an error: the capacity numbers above stay useful to display.
        if (tour.Tour.Status != TourStatus.Planned)
        {
            return new FittingOrdersResponse(tour.Tour.Id, remainingWeightKg, remainingLoadMeters, []);
        }

        IReadOnlyList<TransportOrder> drafts = await orders.ListByStatusAsync(
            OrderStatus.Draft, cancellationToken);

        // Fit means fit alone: two orders that only fit together are a packing decision the
        // dispatcher makes one assignment at a time, with this list recomputed in between.
        OrderResponse[] fitting = drafts
            .Where(order => order.Cargo.WeightKg <= remainingWeightKg
                && order.Cargo.LoadMeters <= remainingLoadMeters)
            .OrderBy(order => order.PickupWindow.From)
            .ThenBy(order => order.OrderNumber.Value, StringComparer.Ordinal)
            .Select(OrderResponse.From)
            .ToArray();

        return new FittingOrdersResponse(tour.Tour.Id, remainingWeightKg, remainingLoadMeters, fitting);
    }
}
