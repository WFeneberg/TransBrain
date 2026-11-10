using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.ListVehicleCatalog;

public sealed record ListVehicleCatalogQuery(VehicleCatalogKind Kind)
    : IQuery<IReadOnlyList<VehicleCatalogEntryResponse>>;