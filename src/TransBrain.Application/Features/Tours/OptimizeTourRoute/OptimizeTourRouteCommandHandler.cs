using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Routing;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours.OptimizeTourRoute;

/// <remarks>
/// The optimizer works on air-line distances between the geocoded stops (see
/// <see cref="StopSequenceOptimizer"/> for why), so this handler needs the geocoder but not the
/// road routing service. The road route over the new order is what GetTourRoute answers next —
/// the UI refetches it after every change to the stops anyway.
/// </remarks>
internal sealed class OptimizeTourRouteCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    IGeoOverrideRepository geoOverrides,
    IGeocoder geocoder,
    ICacheService cache)
    : ICommandHandler<OptimizeTourRouteCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        OptimizeTourRouteCommand command,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            command.Id, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        if (tour.Tour.Stops.Count == 0)
        {
            return Error.Conflict("Tour.NoStops", "A tour without stops has no route to optimize.");
        }

        // Checked before any geocoding: a refusal the domain would issue anyway should not cost
        // external lookups first.
        if (tour.Tour.Status != TourStatus.Planned)
        {
            return Error.Conflict(
                "Tour.NotEditable",
                $"A tour in status '{tour.Tour.Status}' no longer accepts changes to its stops.");
        }

        List<TourStop> currentOrder = tour.Tour.Stops.OrderBy(stop => stop.Sequence).ToList();

        Result<List<SequencedStop>> sequenced = await ResolveAsync(tour, currentOrder, cancellationToken);
        if (!sequenced.IsSuccess)
        {
            return sequenced.Error!;
        }

        IReadOnlyList<int> optimized = StopSequenceOptimizer.Optimize(sequenced.Value);

        // Identity answer means no order was strictly shorter - nothing to persist then, and the
        // unchanged tour is still the correct response.
        if (!optimized.Where((stopIndex, position) => stopIndex != position).Any())
        {
            return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
        }

        List<TourStop> reordered = optimized.Select(stopIndex => currentOrder[stopIndex]).ToList();

        Result<Unit> applied = tour.Tour.ReorderStops(reordered);
        if (!applied.IsSuccess)
        {
            return applied.Error!;
        }

        await tours.SaveChangesAsync(cancellationToken);

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }

    private async Task<Result<List<SequencedStop>>> ResolveAsync(
        TourContext tour,
        IReadOnlyList<TourStop> stops,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, TransportOrder> byId = tour.AssignedOrders.ToDictionary(order => order.Id);
        List<SequencedStop> resolved = [with(capacity: stops.Count)];

        foreach (TourStop stop in stops)
        {
            // An id with no order behind it is a loading inconsistency, reported rather than
            // skipped: optimizing a route that quietly omits a stop would reorder the wrong tour.
            if (!byId.TryGetValue(stop.TransportOrderId, out TransportOrder? order))
            {
                return Error.NotFound(
                    "TransportOrder.NotFound", $"No transport order with id '{stop.TransportOrderId}'.");
            }

            Address address = stop.StopType == StopType.Pickup ? order.Consignor : order.Consignee;
            Result<GeoPoint> point = await LocationResolver.ResolveAsync(
                address, geoOverrides, geocoder, cache, cancellationToken);
            if (!point.IsSuccess)
            {
                return point.Error!;
            }

            resolved.Add(new SequencedStop(
                stop.TransportOrderId, stop.StopType == StopType.Pickup, point.Value));
        }

        return resolved;
    }
}
