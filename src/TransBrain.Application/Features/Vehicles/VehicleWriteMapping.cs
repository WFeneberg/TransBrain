using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <summary>Turns the wire values of a vehicle write into domain values.</summary>
internal static class VehicleWriteMapping
{
    // Enum.TryParse alone accepts any numeric string (e.g. "99") and maps it to the underlying
    // integer value even when no member defines it, silently persisting an undefined enum via
    // HasConversion<string>(). Enum.IsDefined closes that gap.
    public static Result<VehicleType> ParseType(string type) =>
        Enum.TryParse(type, ignoreCase: true, out VehicleType parsed) && Enum.IsDefined(parsed)
            ? parsed
            : Error.Validation("Vehicle.UnknownType", $"'{type}' is not a known vehicle type.");

    /// <param name="current">The vehicle's details before an update; null when a vehicle is created.</param>
    public static async Task<Result<VehicleDetails>> ToDetailsAsync(
        IVehicleWriteCommand command,
        IVehicleCatalogRepository catalog,
        VehicleDetails? current,
        CancellationToken cancellationToken)
    {
        Result<string?> vehicleClass = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.VehicleClass,
            command.VehicleClass,
            current?.VehicleClass,
            nameof(IVehicleWriteCommand.VehicleClass),
            cancellationToken);
        if (!vehicleClass.IsSuccess)
        {
            return vehicleClass.Error!;
        }

        Result<string?> carrierGroup = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.CarrierGroup,
            command.CarrierGroup,
            current?.CarrierGroup,
            nameof(IVehicleWriteCommand.CarrierGroup),
            cancellationToken);
        if (!carrierGroup.IsSuccess)
        {
            return carrierGroup.Error!;
        }

        return VehicleDetails.Create(
            command.IsLicensePlateConfidential,
            command.Make,
            command.Model,
            command.ManufacturedOn,
            command.PowerKw,
            vehicleClass.Value,
            carrierGroup.Value,
            command.ChassisNumber,
            command.EngineNumber,
            command.LengthMm,
            command.WidthMm,
            command.HeightMm,
            command.CurbWeightKg,
            command.GrossWeightKg,
            command.MaxFifthWheelLoadKg,
            command.Notes,
            command.DataSheet);
    }

    /// <param name="current">The vehicle's commercial data before an update; null when a vehicle is created.</param>
    public static async Task<Result<VehicleCommercialInfo>> ToCommercialAsync(
        VehicleCommercialData? data,
        IVehicleCatalogRepository catalog,
        VehicleCommercialInfo? current,
        CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return VehicleCommercialInfo.Empty;
        }

        Result<string?> vehicleInsurer = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.InsuranceCompany,
            data.VehicleInsurer,
            current?.VehicleInsurer,
            CommercialField(nameof(VehicleCommercialData.VehicleInsurer)),
            cancellationToken);
        if (!vehicleInsurer.IsSuccess)
        {
            return vehicleInsurer.Error!;
        }

        Result<string?> transportInsurer = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.InsuranceCompany,
            data.TransportInsurer,
            current?.TransportInsurer,
            CommercialField(nameof(VehicleCommercialData.TransportInsurer)),
            cancellationToken);
        if (!transportInsurer.IsSuccess)
        {
            return transportInsurer.Error!;
        }

        return VehicleCommercialInfo.Create(
            data.PurchasePrice,
            data.PurchasedOn,
            data.PurchaseOdometerKm,
            data.FirstRegisteredOn,
            data.UsefulLifeYears,
            data.UsefulLifeKm,
            data.CostCenter,
            vehicleInsurer.Value,
            data.VehicleInsurancePolicyNumbers,
            transportInsurer.Value,
            data.TransportInsurancePolicyNumbers);
    }

    // Same dotted key FluentValidation produces for the nested rules, so clients map both alike.
    private static string CommercialField(string name) => $"{nameof(IVehicleWriteCommand.Commercial)}.{name}";

    public static Result<VehicleEquipment> ToEquipment(VehicleEquipmentData? data)
    {
        if (data is null)
        {
            return VehicleEquipment.Empty;
        }

        MarbleRackMaterial? material = null;
        if (data.MarbleRackMaterial is not null)
        {
            if (!TryParseMarbleRackMaterial(data.MarbleRackMaterial, out MarbleRackMaterial parsed))
            {
                return Error.Validation(
                    "Vehicle.UnknownMarbleRackMaterial",
                    $"'{data.MarbleRackMaterial}' is not a known marble rack material.");
            }

            material = parsed;
        }

        return VehicleEquipment.Create(
            data.HasPartitionWall,
            data.HasPalletBasket,
            data.HasDangerousGoodsEquipment,
            data.IsDangerousGoodsApproved,
            data.HasLiftAxle,
            data.HasDoubleDeck,
            data.HasDoubleDeckBeams,
            data.HasSmallLadder,
            data.HasLargeLadder,
            data.TankVolumeLiters,
            data.InteriorLengthMm,
            data.InteriorWidthMm,
            data.InteriorHeightMm,
            data.SupportPoleCount,
            data.LoadBarCount,
            data.LashingStrapCount,
            data.PalletCount,
            data.ChainTensionerCount,
            data.MarbleRackCount,
            material);
    }

    // Names only: a numeric string would otherwise parse to the member with that underlying value.
    public static bool TryParseMarbleRackMaterial(string value, out MarbleRackMaterial material)
    {
        material = default;
        return !value.Any(char.IsDigit)
            && Enum.TryParse(value, ignoreCase: true, out material)
            && Enum.IsDefined(material);
    }

    public static async Task<Result<Unit>> EnsureDriverExistsAsync(
        IDriverRepository drivers, Guid? driverId, CancellationToken cancellationToken)
    {
        if (driverId is { } id && await drivers.GetByIdAsync(id, cancellationToken) is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{id}'.");
        }

        return Unit.Value;
    }
}
