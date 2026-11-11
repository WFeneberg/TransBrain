using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Routing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Geodata.ResolveGeodata;

internal sealed class ResolveGeodataCommandHandler(
    IGeoOverrideRepository overrides,
    IGeocoder geocoder,
    ICacheService cache)
    : ICommandHandler<ResolveGeodataCommand, GeodataPositionResponse>
{
    public async Task<Result<GeodataPositionResponse>> Handle(
        ResolveGeodataCommand command,
        CancellationToken cancellationToken)
    {
        // The probe has no address name; the placeholder never leaves this handler.
        Result<Address> address = Address.Create(
            "probe", command.Street, command.PostalCode, command.City, command.Country);
        if (!address.IsSuccess)
        {
            return address.Error!;
        }

        GeoOverride? manual = await overrides.GetByKeyAsync(GeoOverride.BuildKey(address.Value), cancellationToken);
        if (manual is not null)
        {
            return new GeodataPositionResponse(
                manual.Position.Latitude, manual.Position.Longitude, GeodataSources.Manual);
        }

        Result<GeoPoint> resolved = await LocationResolver.ResolveAsync(
            address.Value, overrides, geocoder, cache, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return resolved.Error!;
        }

        return new GeodataPositionResponse(
            resolved.Value.Latitude, resolved.Value.Longitude, GeodataSources.Geocoded);
    }
}
