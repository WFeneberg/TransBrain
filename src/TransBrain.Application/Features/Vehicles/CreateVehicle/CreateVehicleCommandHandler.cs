using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

internal sealed class CreateVehicleCommandHandler(
    IVehicleRepository repository,
    IDriverRepository drivers,
    IVehicleCatalogRepository catalog,
    ICacheService cache,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
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

        Result<VehicleType> type = VehicleWriteMapping.ParseType(command.Type);
        if (!type.IsSuccess)
        {
            return type.Error!;
        }

        Result<VehicleDetails> details = await VehicleWriteMapping.ToDetailsAsync(
            command, catalog, current: null, cancellationToken);
        if (!details.IsSuccess)
        {
            return details.Error!;
        }

        Result<VehicleEquipment> equipment = VehicleWriteMapping.ToEquipment(command.Equipment);
        if (!equipment.IsSuccess)
        {
            return equipment.Error!;
        }

        Result<VehicleCommercialInfo> commercial = await VehicleWriteMapping.ToCommercialAsync(
            command.Commercial, catalog, current: null, cancellationToken);
        if (!commercial.IsSuccess)
        {
            return commercial.Error!;
        }

        Result<Unit> driver = await VehicleWriteMapping.EnsureDriverExistsAsync(
            drivers, command.DefaultDriverId, cancellationToken);
        if (!driver.IsSuccess)
        {
            return driver.Error!;
        }

        if (await repository.ExistsByLicensePlateAsync(plate.Value, excludingId: null, cancellationToken))
        {
            return Error.Conflict(
                "Vehicle.DuplicateLicensePlate",
                $"A vehicle with license plate '{plate.Value}' already exists.");
        }

        Result<Vehicle> vehicle = Vehicle.Create(
            plate.Value,
            type.Value,
            command.PayloadKg,
            command.LoadMeters,
            command.NextInspectionDue,
            details.Value,
            command.DefaultDriverId,
            equipment.Value,
            commercial.Value);

        if (!vehicle.IsSuccess)
        {
            return vehicle.Error!;
        }

        vehicle.Value.RecordModification(currentUser.UserName, timeProvider.GetUtcNow());

        Result<Vehicle> added = await repository.AddAsync(vehicle.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);

        return VehicleResponse.From(added.Value);
    }
}
