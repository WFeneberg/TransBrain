using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <summary>Turns the wire values of a vehicle write into domain values.</summary>
internal static class VehicleWriteMapping
{
    // Enum.TryParse alone accepts any numeric string (e.g. "99") and maps it to the underlying
    // integer value even when no member defines it, silently persisting an undefined enum via
    // HasConversion<string>(). Enum.IsDefined closes that gap.
    public static Result<VehicleType> ParseType(string type) =>
        Enum.TryParse(type, ignoreCase: true, out VehicleType parsed) && Enum.IsDefined(parsed)
            ? parsed
            : Error.Validation("Vehicle.UnknownType", $"'{type}' is not a known vehicle type.");

    public static bool TryParseClass(string value, out VehicleClass vehicleClass) =>
        Enum.TryParse(value, ignoreCase: true, out vehicleClass) && Enum.IsDefined(vehicleClass);

    public static Result<VehicleDetails> ToDetails(IVehicleWriteCommand command)
    {
        VehicleClass? vehicleClass = null;

        if (!string.IsNullOrWhiteSpace(command.VehicleClass))
        {
            if (!TryParseClass(command.VehicleClass, out VehicleClass parsed))
            {
                return Error.Validation(
                    "Vehicle.UnknownClass", $"'{command.VehicleClass}' is not a known vehicle class.");
            }

            vehicleClass = parsed;
        }

        return VehicleDetails.Create(
            command.IsLicensePlateConfidential,
            command.Make,
            command.Model,
            command.ManufacturedOn,
            command.PowerKw,
            vehicleClass,
            command.CarrierGroup,
            command.ChassisNumber,
            command.EngineNumber,
            command.LengthMm,
            command.WidthMm,
            command.HeightMm,
            command.CurbWeightKg,
            command.GrossWeightKg,
            command.MaxFifthWheelLoadKg,
            command.Notes);
    }

    public static async Task<Result<Unit>> EnsureDriverExistsAsync(
        IDriverRepository drivers, Guid? driverId, CancellationToken cancellationToken)
    {
        if (driverId is { } id && await drivers.GetByIdAsync(id, cancellationToken) is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{id}'.");
        }

        return Unit.Value;
    }
}
