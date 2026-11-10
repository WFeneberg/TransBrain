using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.CreateVehicleCatalogEntry;

public sealed record CreateVehicleCatalogEntryCommand(
    VehicleCatalogKind Kind,
    string Code,
    string Name,
    bool IsActive) : ICommand<VehicleCatalogEntryResponse>;