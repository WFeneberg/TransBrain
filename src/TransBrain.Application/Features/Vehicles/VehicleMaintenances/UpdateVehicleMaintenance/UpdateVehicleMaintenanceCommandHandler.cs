using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.UpdateVehicleMaintenance;

internal sealed class UpdateVehicleMaintenanceCommandHandler(
    IVehicleRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateVehicleMaintenanceCommand, VehicleMaintenanceResponse>
{
    public async Task<Result<VehicleMaintenanceResponse>> Handle(
        UpdateVehicleMaintenanceCommand command,
        CancellationToken cancellationToken)
    {
        VehicleMaintenance? maintenance = await repository.GetMaintenanceAsync(
            command.VehicleId, command.MaintenanceId, cancellationToken);
        if (maintenance is null)
        {
            return VehicleMaintenanceErrors.NotFound(command.MaintenanceId);
        }

        if (!VehicleMaintenanceErrors.TryParseKind(command.Kind, out VehicleMaintenanceKind kind))
        {
            return VehicleMaintenanceErrors.UnknownKind(command.Kind);
        }

        Result<VehicleMaintenance> updated = maintenance.Update(
            kind,
            command.PerformedOn,
            command.Description,
            command.Workshop,
            command.OdometerKm,
            command.Cost,
            command.NextDueOn,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return VehicleMaintenanceResponse.From(updated.Value);
    }
}
