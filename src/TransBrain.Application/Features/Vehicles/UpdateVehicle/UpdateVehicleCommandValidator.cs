using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

/// <remarks>The rules are shared with the other vehicle write; see <see cref="VehicleWriteRules"/>.</remarks>
public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        Include(new VehicleWriteRules());
    }
}
