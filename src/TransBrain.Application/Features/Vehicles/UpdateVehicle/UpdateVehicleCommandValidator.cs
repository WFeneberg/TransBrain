using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.UpdateVehicle;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks (<see cref="TransBrain.Domain.Vehicles.LicensePlate.Create"/> and
/// <see cref="TransBrain.Domain.Vehicles.Vehicle.Update"/>): if these two ever disagree, the
/// domain wins and this is the copy to delete.
/// </remarks>
public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(c => c.LicensePlate).NotEmpty().MaximumLength(15);
        RuleFor(c => c.PayloadKg).GreaterThan(0);
        RuleFor(c => c.LoadMeters).GreaterThan(0m);
    }
}
