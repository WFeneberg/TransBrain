using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Orders.GetOrderLocations;

internal sealed class GetOrderLocationsQueryHandler(
    ITransportOrderRepository orders,
    IGeocoder geocoder,
    ICacheService cache)
    : IQueryHandler<GetOrderLocationsQuery, OrderLocationsResponse>
{
    public async Task<Result<OrderLocationsResponse>> Handle(
        GetOrderLocationsQuery query,
        CancellationToken cancellationToken)
    {
        TransportOrder? order = await orders.GetByIdAsync(query.Id, cancellationToken);
        if (order is null)
        {
            return Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{query.Id}'.");
        }

        Result<PlaceResponse?> consignor = await PlaceAsync(order.Consignor, cancellationToken);
        if (!consignor.IsSuccess)
        {
            return consignor.Error!;
        }

        Result<PlaceResponse?> consignee = await PlaceAsync(order.Consignee, cancellationToken);
        if (!consignee.IsSuccess)
        {
            return consignee.Error!;
        }

        return new OrderLocationsResponse(consignor.Value, consignee.Value);
    }

    // A party that matches no place is shown as absent. Any other failure, such as the service
    // being down or unconfigured, is reported, because the map cannot be drawn honestly without it.
    private async Task<Result<PlaceResponse?>> PlaceAsync(Address address, CancellationToken cancellationToken)
    {
        Result<GeoPoint> point = await LocationResolver.ResolveAsync(address, geocoder, cache, cancellationToken);

        if (point.IsSuccess)
        {
            return new PlaceResponse(
                address.Name,
                $"{address.Street}, {address.PostalCode} {address.City}, {address.Country}",
                point.Value.Latitude,
                point.Value.Longitude);
        }

        if (point.Error!.Type != ErrorType.NotFound)
        {
            return point.Error!;
        }

        return (PlaceResponse?)null;
    }
}
