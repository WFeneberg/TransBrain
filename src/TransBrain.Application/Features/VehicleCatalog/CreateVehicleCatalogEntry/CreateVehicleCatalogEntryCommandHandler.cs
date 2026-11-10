using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.CreateVehicleCatalogEntry;

internal sealed class CreateVehicleCatalogEntryCommandHandler(IVehicleCatalogRepository repository)
    : ICommandHandler<CreateVehicleCatalogEntryCommand, VehicleCatalogEntryResponse>
{
    public async Task<Result<VehicleCatalogEntryResponse>> Handle(
        CreateVehicleCatalogEntryCommand command,
        CancellationToken cancellationToken)
    {
        Result<VehicleCatalogEntry> entry = VehicleCatalogEntry.Create(
            command.Kind, command.Code, command.Name, command.IsActive);
        if (!entry.IsSuccess)
        {
            return entry.Error!;
        }

        if (await repository.FindByCodeAsync(command.Kind, entry.Value.Code, cancellationToken) is not null)
        {
            return VehicleCatalogErrors.DuplicateCode(command.Kind, entry.Value.Code);
        }

        Result<VehicleCatalogEntry> added = await repository.AddAsync(entry.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return VehicleCatalogEntryResponse.From(added.Value, usageCount: 0);
    }
}