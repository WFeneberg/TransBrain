using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Geodata.SetGeodataOverride;

/// <remarks>
/// An upsert keyed on the address: the page edits "the position of this address", and whether
/// that is technically an insert or an update is not a distinction an operator should see. The
/// geocode cache is left alone, because the resolver consults overrides before it.
/// </remarks>
internal sealed class SetGeodataOverrideCommandHandler(IGeoOverrideRepository repository, TimeProvider clock)
    : ICommandHandler<SetGeodataOverrideCommand, GeodataOverrideResponse>
{
    public async Task<Result<GeodataOverrideResponse>> Handle(
        SetGeodataOverrideCommand command,
        CancellationToken cancellationToken)
    {
        Result<GeoOverride> candidate = GeoOverride.Create(
            command.Street,
            command.PostalCode,
            command.City,
            command.Country,
            command.Latitude,
            command.Longitude,
            clock.GetUtcNow());
        if (!candidate.IsSuccess)
        {
            return candidate.Error!;
        }

        GeoOverride? existing = await repository.GetByKeyAsync(candidate.Value.AddressKey, cancellationToken);
        if (existing is not null)
        {
            Result<Unit> repositioned = existing.Reposition(command.Latitude, command.Longitude, clock.GetUtcNow());
            if (!repositioned.IsSuccess)
            {
                return repositioned.Error!;
            }

            await repository.SaveChangesAsync(cancellationToken);
            return Response(existing);
        }

        await repository.AddAsync(candidate.Value, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Response(candidate.Value);
    }

    private static GeodataOverrideResponse Response(GeoOverride geoOverride)
        => new(
            geoOverride.Id,
            geoOverride.Street,
            geoOverride.PostalCode,
            geoOverride.City,
            geoOverride.Country,
            geoOverride.Position.Latitude,
            geoOverride.Position.Longitude,
            geoOverride.UpdatedAt);
}
