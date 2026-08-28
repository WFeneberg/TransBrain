using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

public sealed record VehicleResponse(
    Guid Id,
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue,
    string Status)
{
    public static VehicleResponse From(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.LicensePlate.Value,
        vehicle.Type.ToString(),
        vehicle.PayloadKg,
        vehicle.LoadMeters,
        vehicle.NextInspectionDue,
        vehicle.Status.ToString());
}
