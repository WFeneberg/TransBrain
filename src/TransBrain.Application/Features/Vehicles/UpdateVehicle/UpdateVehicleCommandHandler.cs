using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

internal sealed class UpdateVehicleCommandHandler(
    IVehicleRepository repository,
    IDriverRepository drivers,
    IVehicleCatalogRepository catalog,
    ICacheService cache,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
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

        Result<VehicleType> type = VehicleWriteMapping.ParseType(command.Type);
        if (!type.IsSuccess)
        {
            return type.Error!;
        }

        Result<VehicleDetails> details = await VehicleWriteMapping.ToDetailsAsync(
            command, catalog, vehicle.Details, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error!;
        }

        Result<VehicleEquipment> equipment = VehicleWriteMapping.ToEquipment(command.Equipment);
        if (!equipment.IsSuccess)
        {
            return equipment.Error!;
        }

        Result<Unit> driver = await VehicleWriteMapping.EnsureDriverExistsAsync(
            drivers, command.DefaultDriverId, cancellationToken);
        if (!driver.IsSuccess)
        {
            return driver.Error!;
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
            plate.Value,
            type.Value,
            command.PayloadKg,
            command.LoadMeters,
            command.NextInspectionDue,
            details.Value,
            command.DefaultDriverId,
            equipment.Value);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        updated.Value.RecordModification(currentUser.UserName, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);

        return VehicleResponse.From(updated.Value);
    }
}
