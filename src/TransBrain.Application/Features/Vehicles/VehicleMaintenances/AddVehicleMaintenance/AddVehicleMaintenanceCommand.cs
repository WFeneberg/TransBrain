using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.AddVehicleMaintenance;

public sealed record AddVehicleMaintenanceCommand(
    Guid VehicleId,
    string Kind,
    DateOnly PerformedOn,
    string Description,
    string? Workshop = null,
    int? OdometerKm = null,
    decimal? Cost = null,
    DateOnly? NextDueOn = null) : ICommand<VehicleMaintenanceResponse>, IVehicleMaintenanceWriteCommand;
