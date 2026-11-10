using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.UpdateVehicleCatalogEntry;

/// <remarks>The code is not part of an update: vehicles store it, so it is fixed once created.</remarks>
public sealed record UpdateVehicleCatalogEntryCommand(
    VehicleCatalogKind Kind,
    Guid Id,
    string Name,
    bool IsActive) : ICommand<VehicleCatalogEntryResponse>;