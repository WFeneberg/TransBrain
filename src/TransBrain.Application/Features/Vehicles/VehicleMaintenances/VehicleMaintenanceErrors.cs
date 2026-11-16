using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances;

internal static class VehicleMaintenanceErrors
{
    public static Error VehicleNotFound(Guid vehicleId) =>
        Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{vehicleId}'.");

    public static Error NotFound(Guid maintenanceId) =>
        Error.NotFound("VehicleMaintenance.NotFound", $"No maintenance entry with id '{maintenanceId}'.");

    /// <summary>Keyed by the Kind field so the UI can attach the message to its input.</summary>
    public static Error UnknownKind(string kind) => Error.ValidationFailures(new Dictionary<string, string[]>
    {
        [nameof(IVehicleMaintenanceWriteCommand.Kind)] =
            [$"'{kind}' is not a known maintenance kind ({string.Join(", ", Enum.GetNames<VehicleMaintenanceKind>())})."]
    });

    public static bool TryParseKind(string? value, out VehicleMaintenanceKind kind)
    {
        kind = default;
        return !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, ignoreCase: true, out kind)
            && Enum.IsDefined(kind);
    }
}
