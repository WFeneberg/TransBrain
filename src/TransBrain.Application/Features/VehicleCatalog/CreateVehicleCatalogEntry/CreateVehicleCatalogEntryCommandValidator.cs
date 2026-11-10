using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.CreateVehicleCatalogEntry;

/// <remarks>Mirrors <see cref="VehicleCatalogEntry.Create"/> so all field problems are reported at once.</remarks>
public sealed class CreateVehicleCatalogEntryCommandValidator : AbstractValidator<CreateVehicleCatalogEntryCommand>
{
    public CreateVehicleCatalogEntryCommandValidator()
    {
        RuleFor(c => c.Kind).IsInEnum();

        RuleFor(c => c.Code)
            .NotEmpty()
            .MaximumLength(VehicleCatalogEntry.MaxCodeLength)
            .Must(code => code is null || code.Trim().All(VehicleCatalogEntry.IsCodeCharacter))
            .WithMessage("Code may only contain letters, digits, '-' and '_'.");

        RuleFor(c => c.Name).NotEmpty().MaximumLength(VehicleCatalogEntry.MaxNameLength);
    }
}