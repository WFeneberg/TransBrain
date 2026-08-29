using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

internal sealed class UpdateVehicleCommandHandler(IVehicleRepository repository, ICacheService cache)
    : ICommandHandler<UpdateVehicleCommand, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        UpdateVehicleCommand command,
        CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.Id}'.");
        }

        Result<LicensePlate> plate = LicensePlate.Create(command.LicensePlate);
        if (!plate.IsSuccess)
        {
            return plate.Error!;
        }

        if (!Enum.TryParse(command.Type, ignoreCase: true, out VehicleType type) || !Enum.IsDefined(type))
        {
            return Error.Validation("Vehicle.UnknownType", $"'{command.Type}' is not a known vehicle type.");
        }

        // excludingId is what stops a vehicle colliding with its own plate on an update that
        // leaves the plate alone.
        if (await repository.ExistsByLicensePlateAsync(plate.Value, command.Id, cancellationToken))
        {
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{plate.Value}' already exists.");
        }

        Result<Vehicle> updated = vehicle.Update(
            plate.Value, type, command.PayloadKg, command.LoadMeters, command.NextInspectionDue);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);

        return VehicleResponse.From(updated.Value);
    }
}
