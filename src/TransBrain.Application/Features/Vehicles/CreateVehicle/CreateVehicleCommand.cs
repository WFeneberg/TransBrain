using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

public sealed record CreateVehicleCommand(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue,
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
    VehicleEquipmentData? Equipment = null) : ICommand<VehicleResponse>, IVehicleWriteCommand;
