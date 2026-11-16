using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances;

/// <param name="Kind">Name of a <see cref="VehicleMaintenanceKind"/> value, for example "Service".</param>
public sealed record VehicleMaintenanceResponse(
    Guid Id,
    Guid VehicleId,
    string Kind,
    DateOnly PerformedOn,
    string Description,
    string? Workshop,
    int? OdometerKm,
    decimal? Cost,
    DateOnly? NextDueOn,
    DateTimeOffset ModifiedAt,
    string? ModifiedBy)
{
    public static VehicleMaintenanceResponse From(VehicleMaintenance maintenance) => new(
        maintenance.Id,
        maintenance.VehicleId,
        maintenance.Kind.ToString(),
        maintenance.PerformedOn,
        maintenance.Description,
        maintenance.Workshop,
        maintenance.OdometerKm,
        maintenance.Cost,
        maintenance.NextDueOn,
        maintenance.ModifiedAt,
        maintenance.ModifiedBy);
}
