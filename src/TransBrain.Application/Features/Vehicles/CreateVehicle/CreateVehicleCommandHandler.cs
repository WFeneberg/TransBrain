using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

internal sealed class CreateVehicleCommandHandler(IVehicleRepository repository)
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

        if (!Enum.TryParse(command.Type, ignoreCase: true, out VehicleType type))
        {
            return Error.Validation("Vehicle.UnknownType", $"'{command.Type}' is not a known vehicle type.");
        }

        if (await repository.ExistsByLicensePlateAsync(plate.Value, cancellationToken))
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

        await repository.AddAsync(vehicle.Value, cancellationToken);

        return VehicleResponse.From(vehicle.Value);
    }
}
