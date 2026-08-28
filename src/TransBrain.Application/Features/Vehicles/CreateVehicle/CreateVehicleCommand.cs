using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

public sealed record CreateVehicleCommand(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue) : ICommand<VehicleResponse>;
