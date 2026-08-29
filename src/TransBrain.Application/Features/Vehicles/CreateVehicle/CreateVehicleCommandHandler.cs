using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

internal sealed class CreateVehicleCommandHandler(IVehicleRepository repository, ICacheService cache)
    : ICommandHandler<CreateVehicleCommand, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        CreateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        Result<LicensePlate> plate = LicensePlate.Create(command.LicensePlate);
        if (!plate.IsSuccess)
        {
            return plate.Error!;
        }

        // Enum.TryParse alone accepts any numeric string (e.g. "99") and maps it to the
        // underlying integer value even when no member defines it, silently persisting an
        // undefined VehicleType via HasConversion<string>(). Enum.IsDefined closes that gap.
        if (!Enum.TryParse(command.Type, ignoreCase: true, out VehicleType type) || !Enum.IsDefined(type))
        {
            return Error.Validation("Vehicle.UnknownType", $"'{command.Type}' is not a known vehicle type.");
        }

        if (await repository.ExistsByLicensePlateAsync(plate.Value, excludingId: null, cancellationToken))
        {
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{plate.Value}' already exists.");
        }

        Result<Vehicle> vehicle = Vehicle.Create(
            plate.Value,
            type,
            command.PayloadKg,
            command.LoadMeters,
            command.NextInspectionDue);

        if (!vehicle.IsSuccess)
        {
            return vehicle.Error!;
        }

        Result<Vehicle> added = await repository.AddAsync(vehicle.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        await cache.RemoveByPrefixAsync("vehicles:", cancellationToken);

        return VehicleResponse.From(added.Value);
    }
}
