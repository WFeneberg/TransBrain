using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.DeleteVehicleCatalogEntry;

internal sealed class DeleteVehicleCatalogEntryCommandHandler(IVehicleCatalogRepository repository)
    : ICommandHandler<DeleteVehicleCatalogEntryCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        DeleteVehicleCatalogEntryCommand command,
        CancellationToken cancellationToken)
    {
        VehicleCatalogEntry? entry = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null || entry.Kind != command.Kind)
        {
            return VehicleCatalogErrors.NotFound(command.Kind, command.Id);
        }

        // Vehicles keep the code as plain text, so deleting a used entry would leave them pointing at nothing.
        IReadOnlyDictionary<string, int> usage = await repository.CountUsageAsync(command.Kind, cancellationToken);
        int usageCount = usage.GetValueOrDefault(entry.Code);
        if (usageCount > 0)
        {
            return Error.Conflict(
                "VehicleCatalog.InUse",
                $"'{entry.Name}' is used by {usageCount} vehicle(s). Deactivate it instead.");
        }

        await repository.RemoveAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}