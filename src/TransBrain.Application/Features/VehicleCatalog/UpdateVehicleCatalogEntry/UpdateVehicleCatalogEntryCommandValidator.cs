using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.UpdateVehicleCatalogEntry;

public sealed class UpdateVehicleCatalogEntryCommandValidator : AbstractValidator<UpdateVehicleCatalogEntryCommand>
{
    public UpdateVehicleCatalogEntryCommandValidator()
    {
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(VehicleCatalogEntry.MaxNameLength);
    }
}