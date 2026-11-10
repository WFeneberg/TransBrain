using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks (<see cref="TransBrain.Domain.Vehicles.LicensePlate.Create"/>,
/// <see cref="Vehicle.Create"/>, <see cref="VehicleDetails.Create"/>, <see cref="VehicleEquipment.Create"/> and <see cref="VehicleCommercialInfo.Create"/>): if these ever
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
        RuleFor(c => c.VehicleClass).MaximumLength(VehicleCatalogEntry.MaxCodeLength);
        RuleFor(c => c.CarrierGroup).MaximumLength(VehicleDetails.MaxNameLength);
        RuleFor(c => c.ChassisNumber).MaximumLength(VehicleDetails.MaxIdentifierLength);
        RuleFor(c => c.EngineNumber).MaximumLength(VehicleDetails.MaxIdentifierLength);
        RuleFor(c => c.Notes).MaximumLength(VehicleDetails.MaxNotesLength);
        RuleFor(c => c.DataSheet).MaximumLength(VehicleDetails.MaxDataSheetLength);

        RuleFor(c => c.ManufacturedOn)
            .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
            .WithMessage($"The year of manufacture must not be before {VehicleDetails.MinManufactureYear}.");

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

        When(c => c.Equipment is not null, () =>
        {
            RuleFor(c => c.Equipment!.TankVolumeLiters).InclusiveBetween(1, VehicleEquipment.MaxTankVolumeLiters);
            RuleFor(c => c.Equipment!.InteriorLengthMm).InclusiveBetween(1, VehicleDetails.MaxLengthMm);
            RuleFor(c => c.Equipment!.InteriorWidthMm).InclusiveBetween(1, VehicleDetails.MaxWidthMm);
            RuleFor(c => c.Equipment!.InteriorHeightMm).InclusiveBetween(1, VehicleDetails.MaxHeightMm);
            RuleFor(c => c.Equipment!.SupportPoleCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.LoadBarCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.LashingStrapCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.PalletCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.ChainTensionerCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.MarbleRackCount).InclusiveBetween(0, VehicleEquipment.MaxCount);
            RuleFor(c => c.Equipment!.MarbleRackMaterial)
                .Must(material => material is null || VehicleWriteMapping.TryParseMarbleRackMaterial(material, out _))
                .WithMessage("Marble rack material must be 'Steel' or 'Aluminium'.");
        });

        When(c => c.Commercial is not null, () =>
        {
            RuleFor(c => c.Commercial!.PurchasePrice)
                .InclusiveBetween(0m, VehicleCommercialInfo.MaxPurchasePrice)
                .PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(c => c.Commercial!.PurchaseOdometerKm).InclusiveBetween(0, VehicleCommercialInfo.MaxOdometerKm);
            RuleFor(c => c.Commercial!.UsefulLifeYears).InclusiveBetween(1, VehicleCommercialInfo.MaxUsefulLifeYears);
            RuleFor(c => c.Commercial!.UsefulLifeKm).InclusiveBetween(1, VehicleCommercialInfo.MaxOdometerKm);
            RuleFor(c => c.Commercial!.PurchasedOn)
                .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
                .WithMessage($"The purchase date must not be before {VehicleDetails.MinManufactureYear}.");
            RuleFor(c => c.Commercial!.FirstRegisteredOn)
                .Must(date => date is null || date.Value.Year >= VehicleDetails.MinManufactureYear)
                .WithMessage($"The first registration must not be before {VehicleDetails.MinManufactureYear}.");
            RuleFor(c => c.Commercial!.CostCenter).MaximumLength(VehicleCommercialInfo.MaxCostCenterLength);
            RuleFor(c => c.Commercial!.VehicleInsurer).MaximumLength(VehicleCatalogEntry.MaxCodeLength);
            RuleFor(c => c.Commercial!.TransportInsurer).MaximumLength(VehicleCatalogEntry.MaxCodeLength);
            RuleFor(c => c.Commercial!.VehicleInsurancePolicyNumbers)
                .MaximumLength(VehicleCommercialInfo.MaxPolicyNumbersLength);
            RuleFor(c => c.Commercial!.TransportInsurancePolicyNumbers)
                .MaximumLength(VehicleCommercialInfo.MaxPolicyNumbersLength);
        });
    }
}
