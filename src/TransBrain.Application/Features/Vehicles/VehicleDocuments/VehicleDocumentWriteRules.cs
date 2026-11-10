using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments;

/// <remarks>Mirrors the checks of <see cref="VehicleDocument.Update"/>; if they disagree, the domain wins.</remarks>
internal sealed class VehicleDocumentWriteRules : AbstractValidator<IVehicleDocumentWriteCommand>
{
    public VehicleDocumentWriteRules()
    {
        RuleFor(c => c.DocumentType).NotEmpty().MaximumLength(VehicleCatalogEntry.MaxCodeLength);
        RuleFor(c => c.Remark).MaximumLength(VehicleDocument.MaxRemarkLength);

        RuleFor(c => c.LastCheckedOn)
            .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
            .WithMessage($"The last check must not be before {VehicleDetails.MinManufactureYear}.");

        RuleFor(c => c.ValidUntil)
            .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
            .WithMessage($"The expiry date must not be before {VehicleDetails.MinManufactureYear}.");

        RuleFor(c => c.ValidUntil)
            .Must((command, validUntil) => validUntil >= command.LastCheckedOn)
            .When(c => c.ValidUntil.HasValue && c.LastCheckedOn.HasValue)
            .WithMessage("The document must not expire before it was last checked.");
    }
}