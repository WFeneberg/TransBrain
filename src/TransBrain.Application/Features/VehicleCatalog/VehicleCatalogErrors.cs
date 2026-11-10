using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog;

internal static class VehicleCatalogErrors
{
    public static Error NotFound(VehicleCatalogKind kind, Guid id) =>
        Error.NotFound("VehicleCatalog.NotFound", $"No {Describe(kind)} entry with id '{id}'.");

    public static Error DuplicateCode(VehicleCatalogKind kind, string code) =>
        Error.Conflict("VehicleCatalog.DuplicateCode", $"A {Describe(kind)} with code '{code}' already exists.");

    public static string Describe(VehicleCatalogKind kind) => kind switch
    {
        VehicleCatalogKind.VehicleClass => "vehicle class",
        VehicleCatalogKind.CarrierGroup => "carrier group",
        VehicleCatalogKind.InsuranceCompany => "insurance company",
        VehicleCatalogKind.DocumentType => "document type",
        VehicleCatalogKind.CustomerClass => "customer class",
        _ => kind.ToString()
    };

    /// <summary>What a catalog entry's usage count counts, for messages such as "used by 2 customer(s)".</summary>
    public static string UsageUnit(VehicleCatalogKind kind) =>
        kind == VehicleCatalogKind.CustomerClass ? "customer(s)" : "vehicle(s)";
}