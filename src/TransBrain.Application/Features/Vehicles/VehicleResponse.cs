using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

public sealed record VehicleResponse(
    Guid Id,
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue,
    string Status,
    bool IsLicensePlateConfidential = false,
    string? Make = null,
    string? Model = null,
    DateOnly? ManufacturedOn = null,
    int? PowerKw = null,
    string? VehicleClass = null,
    string? CarrierGroup = null,
    Guid? DefaultDriverId = null,
    string? ChassisNumber = null,
    string? EngineNumber = null,
    int? LengthMm = null,
    int? WidthMm = null,
    int? HeightMm = null,
    int? CurbWeightKg = null,
    int? GrossWeightKg = null,
    int? MaxFifthWheelLoadKg = null,
    string? Notes = null,
    DateTimeOffset? ModifiedAt = null,
    string? ModifiedBy = null)
{
    public static VehicleResponse From(Vehicle vehicle)
    {
        VehicleDetails details = vehicle.Details;

        return new VehicleResponse(
            vehicle.Id,
            vehicle.LicensePlate.Value,
            vehicle.Type.ToString(),
            vehicle.PayloadKg,
            vehicle.LoadMeters,
            vehicle.NextInspectionDue,
            vehicle.Status.ToString(),
            details.IsLicensePlateConfidential,
            details.Make,
            details.Model,
            details.ManufacturedOn,
            details.PowerKw,
            details.VehicleClass,
            details.CarrierGroup,
            vehicle.DefaultDriverId,
            details.ChassisNumber,
            details.EngineNumber,
            details.LengthMm,
            details.WidthMm,
            details.HeightMm,
            details.CurbWeightKg,
            details.GrossWeightKg,
            details.MaxFifthWheelLoadKg,
            details.Notes,
            vehicle.ModifiedAt,
            vehicle.ModifiedBy);
    }
}
