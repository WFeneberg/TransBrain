using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Routing;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours.GetTourRoute;

internal sealed class GetTourRouteQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser,
    IGeoOverrideRepository geoOverrides,
    IGeocoder geocoder,
    IRoutePlanner routePlanner,
    ICacheService cache)
    : IQueryHandler<GetTourRouteQuery, TourRouteResponse>
{
    public async Task<Result<TourRouteResponse>> Handle(
        GetTourRouteQuery query,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            query.Id, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        if (!TourAccess.MaySee(tour.Tour, tour.Driver, currentUser))
        {
            return Error.Forbidden("Tour.NotYours", "A driver may only see their own tours.");
        }

        if (tour.Tour.Stops.Count == 0)
        {
            return Error.Conflict("Tour.NoStops", "A tour without stops has no route.");
        }

        Result<List<PlannedStop>> stops = await ResolveStopsAsync(tour, cancellationToken);
        if (!stops.IsSuccess)
        {
            return stops.Error!;
        }

        RoadRoute? road = null;
        if (stops.Value.Count > 1)
        {
            Result<RoadRoute> planned = await routePlanner.PlanAsync(
                stops.Value.Select(stop => stop.Point).ToArray(), cancellationToken);
            if (!planned.IsSuccess)
            {
                return planned.Error!;
            }

            road = planned.Value;
        }

        // Leg i starts at stop i, so the leg is driven in the country of stop i.
        List<PlannedLeg> legs = [];
        for (int i = 0; road is not null && i < road.Segments.Count; i++)
        {
            legs.Add(new PlannedLeg(stops.Value[i].Address.Country, road.Segments[i].Duration));
        }

        DateTimeOffset departure = query.Departure ?? DefaultDeparture(tour.Tour.TourDate);
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            departure,
            legs,
            appliesNationalTruckBans: true);

        RouteStopResponse[] stopResponses = stops.Value
            .Select(stop => new RouteStopResponse(
                stop.Sequence,
                stop.OrderNumber,
                stop.StopType.ToString(),
                stop.Address.Country,
                $"{stop.Address.Name}, {stop.Address.City}",
                stop.Point.Latitude,
                stop.Point.Longitude))
            .ToArray();

        IReadOnlyList<GeoPoint> path = road?.Geometry ?? stops.Value.Select(stop => stop.Point).ToArray();
        RoutePointResponse[] geometry = path
            .Select(point => new RoutePointResponse(point.Latitude, point.Longitude))
            .ToArray();

        double distanceMeters = road?.Segments.Sum(segment => segment.DistanceMeters) ?? 0;

        return TourRouteResponse.From(tour.Tour.Id, departure, schedule, stopResponses, geometry, distanceMeters);
    }

    private async Task<Result<List<PlannedStop>>> ResolveStopsAsync(TourContext tour, CancellationToken cancellationToken)
    {
        Dictionary<Guid, TransportOrder> byId = tour.AssignedOrders.ToDictionary(order => order.Id);
        List<PlannedStop> resolved = [];

        foreach (TourStop stop in tour.Tour.Stops.OrderBy(s => s.Sequence))
        {
            // An id with no order behind it is a loading inconsistency, reported rather than skipped:
            // a route that quietly omits a stop would look valid and be wrong.
            if (!byId.TryGetValue(stop.TransportOrderId, out TransportOrder? order))
            {
                return Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{stop.TransportOrderId}'.");
            }

            Address address = stop.StopType == StopType.Pickup ? order.Consignor : order.Consignee;
            Result<GeoPoint> point = await LocationResolver.ResolveAsync(
                address, geoOverrides, geocoder, cache, cancellationToken);
            if (!point.IsSuccess)
            {
                return point.Error!;
            }

            resolved.Add(new PlannedStop(stop.Sequence, order.OrderNumber.Value, stop.StopType, address, point.Value));
        }

        return resolved;
    }

    private static DateTimeOffset DefaultDeparture(DateOnly tourDate)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(DrivingRules.DispatchTimeZoneId);
        DateTime local = tourDate.ToDateTime(new TimeOnly(6, 0));
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    private sealed record PlannedStop(
        int Sequence,
        string OrderNumber,
        StopType StopType,
        Address Address,
        GeoPoint Point);
}
