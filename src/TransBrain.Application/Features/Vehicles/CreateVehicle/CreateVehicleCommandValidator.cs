using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.CreateVehicle;

public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(c => c.LicensePlate).NotEmpty().MaximumLength(15);
        RuleFor(c => c.Type).NotEmpty();
        RuleFor(c => c.PayloadKg).GreaterThan(0);
        RuleFor(c => c.LoadMeters).GreaterThan(0m);
        RuleFor(c => c.NextInspectionDue).NotEmpty();
    }
}
