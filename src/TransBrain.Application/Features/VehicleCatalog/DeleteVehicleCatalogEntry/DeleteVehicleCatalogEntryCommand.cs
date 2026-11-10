using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.DeleteVehicleCatalogEntry;

public sealed record DeleteVehicleCatalogEntryCommand(VehicleCatalogKind Kind, Guid Id) : ICommand<Unit>;