using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

/// <remarks>The rules are shared with the other vehicle write; see <see cref="VehicleWriteRules"/>.</remarks>
public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        Include(new VehicleWriteRules());
    }
}
