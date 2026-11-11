using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Geodata.DeleteGeodataOverride;

internal sealed class DeleteGeodataOverrideCommandHandler(IGeoOverrideRepository repository)
    : ICommandHandler<DeleteGeodataOverrideCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        DeleteGeodataOverrideCommand command,
        CancellationToken cancellationToken)
    {
        GeoOverride? geoOverride = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (geoOverride is null)
        {
            return Error.NotFound("GeoOverride.NotFound", $"No geodata override with id '{command.Id}'.");
        }

        await repository.RemoveAsync(geoOverride, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
