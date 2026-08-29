using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid Id,
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue) : ICommand<VehicleResponse>;
