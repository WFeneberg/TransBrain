using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks (<see cref="TransBrain.Domain.Vehicles.LicensePlate.Create"/>,
/// <see cref="Vehicle.Create"/> and <see cref="VehicleDetails.Create"/>): if these ever
/// disagree, the domain wins and this is the copy to fix.
/// </remarks>
internal sealed class VehicleWriteRules : AbstractValidator<IVehicleWriteCommand>
{
    public VehicleWriteRules()
    {
        RuleFor(c => c.LicensePlate).NotEmpty().MaximumLength(15);
        RuleFor(c => c.PayloadKg).GreaterThan(0);
        RuleFor(c => c.LoadMeters).GreaterThan(0m);

        RuleFor(c => c.Make).MaximumLength(VehicleDetails.MaxNameLength);
        RuleFor(c => c.Model).MaximumLength(VehicleDetails.MaxNameLength);
        RuleFor(c => c.CarrierGroup).MaximumLength(VehicleDetails.MaxNameLength);
        RuleFor(c => c.ChassisNumber).MaximumLength(VehicleDetails.MaxIdentifierLength);
        RuleFor(c => c.EngineNumber).MaximumLength(VehicleDetails.MaxIdentifierLength);
        RuleFor(c => c.Notes).MaximumLength(VehicleDetails.MaxNotesLength);

        RuleFor(c => c.ManufacturedOn)
            .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
            .WithMessage($"The year of manufacture must not be before {VehicleDetails.MinManufactureYear}.");

        RuleFor(c => c.VehicleClass)
            .Must(value => string.IsNullOrWhiteSpace(value) || VehicleWriteMapping.TryParseClass(value, out _))
            .WithMessage(c => $"'{c.VehicleClass}' is not a known vehicle class.");

        RuleFor(c => c.PowerKw).InclusiveBetween(1, VehicleDetails.MaxPowerKw);
        RuleFor(c => c.LengthMm).InclusiveBetween(1, VehicleDetails.MaxLengthMm);
        RuleFor(c => c.WidthMm).InclusiveBetween(1, VehicleDetails.MaxWidthMm);
        RuleFor(c => c.HeightMm).InclusiveBetween(1, VehicleDetails.MaxHeightMm);
        RuleFor(c => c.CurbWeightKg).InclusiveBetween(1, VehicleDetails.MaxWeightKg);
        RuleFor(c => c.GrossWeightKg).InclusiveBetween(1, VehicleDetails.MaxWeightKg);
        RuleFor(c => c.MaxFifthWheelLoadKg).InclusiveBetween(1, VehicleDetails.MaxWeightKg);

        RuleFor(c => c.GrossWeightKg)
            .Must((command, gross) => gross > command.CurbWeightKg)
            .When(c => c.GrossWeightKg.HasValue && c.CurbWeightKg.HasValue)
            .WithMessage("Gross weight must be greater than the curb weight.");
    }
}
