namespace TransBrain.Application.Features.Vehicles;

/// <summary>
/// The fields create and update share, so both commands are validated and mapped onto the domain
/// by one set of rules (<see cref="VehicleWriteRules"/>, <see cref="VehicleWriteMapping"/>).
/// </summary>
public interface IVehicleWriteCommand
{
    string LicensePlate { get; }

    string Type { get; }

    int PayloadKg { get; }

    decimal LoadMeters { get; }

    DateOnly NextInspectionDue { get; }

    bool IsLicensePlateConfidential { get; }

    string? Make { get; }

    string? Model { get; }

    DateOnly? ManufacturedOn { get; }

    int? PowerKw { get; }

    string? VehicleClass { get; }

    string? CarrierGroup { get; }

    Guid? DefaultDriverId { get; }

    string? ChassisNumber { get; }

    string? EngineNumber { get; }

    int? LengthMm { get; }

    int? WidthMm { get; }

    int? HeightMm { get; }

    int? CurbWeightKg { get; }

    int? GrossWeightKg { get; }

    int? MaxFifthWheelLoadKg { get; }

    string? Notes { get; }
}
