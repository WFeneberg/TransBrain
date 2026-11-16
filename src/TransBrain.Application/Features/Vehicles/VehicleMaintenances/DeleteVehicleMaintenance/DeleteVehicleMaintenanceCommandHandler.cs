using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.DeleteVehicleMaintenance;

internal sealed class DeleteVehicleMaintenanceCommandHandler(IVehicleRepository repository)
    : ICommandHandler<DeleteVehicleMaintenanceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        DeleteVehicleMaintenanceCommand command, CancellationToken cancellationToken)
    {
        VehicleMaintenance? maintenance = await repository.GetMaintenanceAsync(
            command.VehicleId, command.MaintenanceId, cancellationToken);
        if (maintenance is null)
        {
            return VehicleMaintenanceErrors.NotFound(command.MaintenanceId);
        }

        await repository.RemoveMaintenanceAsync(maintenance, cancellationToken);

        return Unit.Value;
    }
}
