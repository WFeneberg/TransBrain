using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.UpdateVehicleMaintenance;

/// <remarks>The rules are shared with the add; see <see cref="VehicleMaintenanceWriteRules"/>.</remarks>
public sealed class UpdateVehicleMaintenanceCommandValidator : AbstractValidator<UpdateVehicleMaintenanceCommand>
{
    public UpdateVehicleMaintenanceCommandValidator()
    {
        Include(new VehicleMaintenanceWriteRules());
    }
}
