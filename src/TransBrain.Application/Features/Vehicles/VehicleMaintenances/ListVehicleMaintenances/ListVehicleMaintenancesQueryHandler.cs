using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.ListVehicleMaintenances;

internal sealed class ListVehicleMaintenancesQueryHandler(IVehicleRepository repository)
    : IQueryHandler<ListVehicleMaintenancesQuery, VehicleMaintenanceResponse[]>
{
    public async Task<Result<VehicleMaintenanceResponse[]>> Handle(
        ListVehicleMaintenancesQuery query,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(query.VehicleId, cancellationToken) is null)
        {
            return VehicleMaintenanceErrors.VehicleNotFound(query.VehicleId);
        }

        IReadOnlyList<VehicleMaintenance> maintenances =
            await repository.ListMaintenancesAsync(query.VehicleId, cancellationToken);

        return maintenances.Select(VehicleMaintenanceResponse.From).ToArray();
    }
}
