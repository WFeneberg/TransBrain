using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.UpdateVehicleMaintenance;

public sealed record UpdateVehicleMaintenanceCommand(
    Guid VehicleId,
    Guid MaintenanceId,
    string Kind,
    DateOnly PerformedOn,
    string Description,
    string? Workshop = null,
    int? OdometerKm = null,
    decimal? Cost = null,
    DateOnly? NextDueOn = null) : ICommand<VehicleMaintenanceResponse>, IVehicleMaintenanceWriteCommand;
