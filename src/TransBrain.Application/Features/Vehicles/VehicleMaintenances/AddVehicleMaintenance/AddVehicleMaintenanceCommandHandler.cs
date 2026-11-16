using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.AddVehicleMaintenance;

internal sealed class AddVehicleMaintenanceCommandHandler(
    IVehicleRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<AddVehicleMaintenanceCommand, VehicleMaintenanceResponse>
{
    public async Task<Result<VehicleMaintenanceResponse>> Handle(
        AddVehicleMaintenanceCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(command.VehicleId, cancellationToken) is null)
        {
            return VehicleMaintenanceErrors.VehicleNotFound(command.VehicleId);
        }

        if (!VehicleMaintenanceErrors.TryParseKind(command.Kind, out VehicleMaintenanceKind kind))
        {
            return VehicleMaintenanceErrors.UnknownKind(command.Kind);
        }

        Result<VehicleMaintenance> maintenance = VehicleMaintenance.Create(
            command.VehicleId,
            kind,
            command.PerformedOn,
            command.Description,
            command.Workshop,
            command.OdometerKm,
            command.Cost,
            command.NextDueOn,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!maintenance.IsSuccess)
        {
            return maintenance.Error!;
        }

        await repository.AddMaintenanceAsync(maintenance.Value, cancellationToken);

        return VehicleMaintenanceResponse.From(maintenance.Value);
    }
}
