using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// The purchase, depreciation and insurance data of a vehicle ("kaufmännische Daten"). Accounting
/// uses it for depreciation and cost allocation, dispatch to know who insures the vehicle and the load.
/// Every value is optional. Amounts are in EUR.
/// </summary>
public sealed record VehicleCommercialInfo
{
    public const decimal MaxPurchasePrice = 10_000_000m;
    public const int MaxOdometerKm = 9_999_999;
    public const int MaxUsefulLifeYears = 50;
    public const int MaxCostCenterLength = 30;
    public const int MaxPolicyNumbersLength = 500;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static VehicleCommercialInfo Empty => new();

    private VehicleCommercialInfo()
    {
    }

    /// <summary>Purchase price in EUR ("Anschaffungspreis").</summary>
    public decimal? PurchasePrice { get; private init; }

    /// <summary>Date of purchase ("Anschaffungsdatum").</summary>
    public DateOnly? PurchasedOn { get; private init; }

    /// <summary>Odometer reading when the vehicle was bought ("Anschaffungs-km").</summary>
    public int? PurchaseOdometerKm { get; private init; }

    /// <summary>Date of first registration ("Erstzulassung"); may differ from the year of manufacture.</summary>
    public DateOnly? FirstRegisteredOn { get; private init; }

    /// <summary>Planned useful life in years ("Nutzungsdauer Jahre").</summary>
    public int? UsefulLifeYears { get; private init; }

    /// <summary>Planned useful life in kilometres ("Nutzungsdauer km").</summary>
    public int? UsefulLifeKm { get; private init; }

    /// <summary>Cost center the vehicle's costs are booked to ("Kostenstelle").</summary>
    public string? CostCenter { get; private init; }

    /// <summary>Code of the vehicle insurer from the insurance company catalog.</summary>
    public string? VehicleInsurer { get; private init; }

    /// <summary>Policy number(s) of the vehicle insurance, one per line.</summary>
    public string? VehicleInsurancePolicyNumbers { get; private init; }

    /// <summary>Code of the transport (cargo) insurer from the insurance company catalog.</summary>
    public string? TransportInsurer { get; private init; }

    /// <summary>Policy number(s) of the transport insurance, one per line.</summary>
    public string? TransportInsurancePolicyNumbers { get; private init; }

    public static Result<VehicleCommercialInfo> Create(
        decimal? purchasePrice = null,
        DateOnly? purchasedOn = null,
        int? purchaseOdometerKm = null,
        DateOnly? firstRegisteredOn = null,
        int? usefulLifeYears = null,
        int? usefulLifeKm = null,
        string? costCenter = null,
        string? vehicleInsurer = null,
        string? vehicleInsurancePolicyNumbers = null,
        string? transportInsurer = null,
        string? transportInsurancePolicyNumbers = null)
    {
        VehicleCommercialInfo info = new()
        {
            PurchasePrice = purchasePrice,
            PurchasedOn = purchasedOn,
            PurchaseOdometerKm = purchaseOdometerKm,
            FirstRegisteredOn = firstRegisteredOn,
            UsefulLifeYears = usefulLifeYears,
            UsefulLifeKm = usefulLifeKm,
            CostCenter = OptionalText.Normalize(costCenter),
            VehicleInsurer = OptionalText.Normalize(vehicleInsurer),
            VehicleInsurancePolicyNumbers = OptionalText.Normalize(vehicleInsurancePolicyNumbers),
            TransportInsurer = OptionalText.Normalize(transportInsurer),
            TransportInsurancePolicyNumbers = OptionalText.Normalize(transportInsurancePolicyNumbers),
        };

        Error? error = info.Validate();
        return error is null ? info : error;
    }

    private Error? Validate()
    {
        if (PurchasePrice is { } price && (price < 0m || price > MaxPurchasePrice || decimal.Round(price, 2) != price))
        {
            return Error.Validation(
                "Vehicle.PurchasePriceOutOfRange",
                $"The purchase price must be between 0 and {MaxPurchasePrice:0} with at most two decimals.");
        }

        if (PurchaseOdometerKm is < 0 or > MaxOdometerKm)
        {
            return Error.Validation(
                "Vehicle.PurchaseOdometerOutOfRange", $"The odometer reading must be between 0 and {MaxOdometerKm} km.");
        }

        if (UsefulLifeYears is < 1 or > MaxUsefulLifeYears || UsefulLifeKm is < 1 or > MaxOdometerKm)
        {
            return Error.Validation(
                "Vehicle.UsefulLifeOutOfRange",
                $"The useful life must be between 1 and {MaxUsefulLifeYears} years and between 1 and {MaxOdometerKm} km.");
        }

        if (PurchasedOn is { Year: < VehicleDetails.MinManufactureYear }
            || FirstRegisteredOn is { Year: < VehicleDetails.MinManufactureYear })
        {
            return Error.Validation(
                "Vehicle.CommercialDateTooEarly",
                $"Purchase and first registration date must not be before {VehicleDetails.MinManufactureYear}.");
        }

        if (CostCenter is { Length: > MaxCostCenterLength })
        {
            return Error.Validation(
                "Vehicle.CostCenterTooLong", $"The cost center must not exceed {MaxCostCenterLength} characters.");
        }

        if (VehicleInsurer is { Length: > VehicleCatalogEntry.MaxCodeLength }
            || TransportInsurer is { Length: > VehicleCatalogEntry.MaxCodeLength })
        {
            return Error.Validation(
                "Vehicle.InsurerTooLong",
                $"An insurer code must not exceed {VehicleCatalogEntry.MaxCodeLength} characters.");
        }

        if (VehicleInsurancePolicyNumbers is { Length: > MaxPolicyNumbersLength }
            || TransportInsurancePolicyNumbers is { Length: > MaxPolicyNumbersLength })
        {
            return Error.Validation(
                "Vehicle.PolicyNumbersTooLong",
                $"Policy numbers must not exceed {MaxPolicyNumbersLength} characters.");
        }

        return null;
    }
}
