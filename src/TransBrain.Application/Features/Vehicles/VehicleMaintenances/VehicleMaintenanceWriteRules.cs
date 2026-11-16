using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleMaintenances;

/// <remarks>Mirrors the checks of <see cref="VehicleMaintenance.Update"/>; if they disagree, the domain wins.</remarks>
internal sealed class VehicleMaintenanceWriteRules : AbstractValidator<IVehicleMaintenanceWriteCommand>
{
    public VehicleMaintenanceWriteRules()
    {
        RuleFor(c => c.Kind)
            .Must(kind => VehicleMaintenanceErrors.TryParseKind(kind, out _))
            .WithMessage(c =>
                $"'{c.Kind}' is not a known maintenance kind "
                + $"({string.Join(", ", Enum.GetNames<VehicleMaintenanceKind>())}).");

        RuleFor(c => c.PerformedOn)
            .Must(date => date.Year >= VehicleDetails.MinManufactureYear)
            .WithMessage($"The maintenance date must not be before {VehicleDetails.MinManufactureYear}.");

        RuleFor(c => c.Description).NotEmpty().MaximumLength(VehicleMaintenance.MaxDescriptionLength);
        RuleFor(c => c.Workshop).MaximumLength(VehicleMaintenance.MaxWorkshopLength);

        RuleFor(c => c.OdometerKm)
            .InclusiveBetween(0, VehicleMaintenance.MaxOdometerKm)
            .When(c => c.OdometerKm.HasValue);

        RuleFor(c => c.Cost)
            .InclusiveBetween(0m, VehicleMaintenance.MaxCost)
            .When(c => c.Cost.HasValue);

        RuleFor(c => c.NextDueOn)
            .Must((command, nextDueOn) => nextDueOn >= command.PerformedOn)
            .When(c => c.NextDueOn.HasValue)
            .WithMessage("The next due date must not be before the maintenance date.");
    }
}
