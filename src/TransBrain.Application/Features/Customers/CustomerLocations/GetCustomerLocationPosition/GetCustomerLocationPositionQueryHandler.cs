using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Customers.CustomerLocations.GetCustomerLocationPosition;

internal sealed class GetCustomerLocationPositionQueryHandler(
    ICustomerRepository repository,
    IGeocoder geocoder,
    ICacheService cache)
    : IQueryHandler<GetCustomerLocationPositionQuery, CustomerLocationPositionResponse>
{
    public async Task<Result<CustomerLocationPositionResponse>> Handle(
        GetCustomerLocationPositionQuery query,
        CancellationToken cancellationToken)
    {
        CustomerLocation? location = await repository.GetLocationAsync(
            query.CustomerId, query.LocationId, cancellationToken);
        if (location is null)
        {
            return Error.NotFound("CustomerLocation.NotFound", $"No delivery location with id '{query.LocationId}'.");
        }

        // Errors pass through unchanged: NotFound means the address matches no place, Unavailable
        // means the geocoding service is not configured or not reachable. The caller needs to tell
        // those apart, because only the second is fixed by configuration.
        Result<GeoPoint> point = await LocationResolver.ResolveAsync(location.Address, geocoder, cache, cancellationToken);
        if (!point.IsSuccess)
        {
            return point.Error!;
        }

        Address address = location.Address;
        return new CustomerLocationPositionResponse(
            location.Name,
            $"{address.Street}, {address.PostalCode} {address.City}, {address.Country}",
            point.Value.Latitude,
            point.Value.Longitude);
    }
}
