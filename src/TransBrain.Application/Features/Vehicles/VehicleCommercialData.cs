using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <summary>
/// Wire shape of <see cref="VehicleCommercialInfo"/>, used in both the vehicle write bodies and
/// <see cref="VehicleResponse"/>. <see cref="VehicleInsurer"/> and <see cref="TransportInsurer"/> are
/// codes of the insurance company catalog.
/// </summary>
public sealed record VehicleCommercialData(
    decimal? PurchasePrice = null,
    DateOnly? PurchasedOn = null,
    int? PurchaseOdometerKm = null,
    DateOnly? FirstRegisteredOn = null,
    int? UsefulLifeYears = null,
    int? UsefulLifeKm = null,
    string? CostCenter = null,
    string? VehicleInsurer = null,
    string? VehicleInsurancePolicyNumbers = null,
    string? TransportInsurer = null,
    string? TransportInsurancePolicyNumbers = null)
{
    public static VehicleCommercialData From(VehicleCommercialInfo info) => new(
        info.PurchasePrice,
        info.PurchasedOn,
        info.PurchaseOdometerKm,
        info.FirstRegisteredOn,
        info.UsefulLifeYears,
        info.UsefulLifeKm,
        info.CostCenter,
        info.VehicleInsurer,
        info.VehicleInsurancePolicyNumbers,
        info.TransportInsurer,
        info.TransportInsurancePolicyNumbers);
}
