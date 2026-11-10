using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.UpdateVehicleCatalogEntry;

internal sealed class UpdateVehicleCatalogEntryCommandHandler(IVehicleCatalogRepository repository)
    : ICommandHandler<UpdateVehicleCatalogEntryCommand, VehicleCatalogEntryResponse>
{
    public async Task<Result<VehicleCatalogEntryResponse>> Handle(
        UpdateVehicleCatalogEntryCommand command,
        CancellationToken cancellationToken)
    {
        VehicleCatalogEntry? entry = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null || entry.Kind != command.Kind)
        {
            return VehicleCatalogErrors.NotFound(command.Kind, command.Id);
        }

        Result<VehicleCatalogEntry> updated = entry.Update(command.Name, command.IsActive);
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        IReadOnlyDictionary<string, int> usage = await repository.CountUsageAsync(command.Kind, cancellationToken);
        return VehicleCatalogEntryResponse.From(updated.Value, usage.GetValueOrDefault(entry.Code));
    }
}