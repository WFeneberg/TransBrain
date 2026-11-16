using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.DeleteVehicleMaintenance;

public sealed record DeleteVehicleMaintenanceCommand(Guid VehicleId, Guid MaintenanceId) : ICommand<Unit>;
