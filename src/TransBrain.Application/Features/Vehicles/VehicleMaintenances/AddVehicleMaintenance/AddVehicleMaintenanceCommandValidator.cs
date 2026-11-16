using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances.AddVehicleMaintenance;

/// <remarks>The rules are shared with the update; see <see cref="VehicleMaintenanceWriteRules"/>.</remarks>
public sealed class AddVehicleMaintenanceCommandValidator : AbstractValidator<AddVehicleMaintenanceCommand>
{
    public AddVehicleMaintenanceCommandValidator()
    {
        Include(new VehicleMaintenanceWriteRules());
    }
}
