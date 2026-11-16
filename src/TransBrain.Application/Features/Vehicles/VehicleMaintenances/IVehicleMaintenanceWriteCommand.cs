namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances;

/// <summary>The fields adding and updating a maintenance entry share.</summary>
public interface IVehicleMaintenanceWriteCommand
{
    Guid VehicleId { get; }

    /// <summary>Name of a <see cref="Domain.Vehicles.VehicleMaintenanceKind"/> value (case-insensitive).</summary>
    string Kind { get; }

    DateOnly PerformedOn { get; }

    string Description { get; }

    string? Workshop { get; }

    int? OdometerKm { get; }

    decimal? Cost { get; }

    DateOnly? NextDueOn { get; }
}
