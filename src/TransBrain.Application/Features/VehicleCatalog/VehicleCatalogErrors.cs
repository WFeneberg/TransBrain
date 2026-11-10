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
        _ => kind.ToString()
    };
}