using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.ChangeVehicleStatus;

internal sealed class ChangeVehicleStatusCommandHandler(
    IVehicleRepository repository,
    ICacheService cache,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeVehicleStatusCommand, VehicleResponse>
{
    public static bool TryParseStatus(string? value, out VehicleStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    public async Task<Result<VehicleResponse>> Handle(
        ChangeVehicleStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryParseStatus(command.Status, out VehicleStatus target))
        {
            return Error.Validation("Vehicle.UnknownStatus", $"'{command.Status}' is not a known vehicle status.");
        }

        Vehicle? vehicle = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.Id}'.");
        }

        if (vehicle.Status == target)
        {
            return VehicleResponse.From(vehicle);
        }

        Result<Vehicle> changed = vehicle.ChangeStatus(target);
        if (!changed.IsSuccess)
        {
            return changed.Error!;
        }

        vehicle.RecordModification(currentUser.UserName, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);

        return VehicleResponse.From(vehicle);
    }
}
