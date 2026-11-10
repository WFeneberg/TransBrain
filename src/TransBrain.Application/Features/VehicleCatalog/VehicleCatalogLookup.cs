using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog;

/// <summary>Resolves the catalog codes a vehicle write refers to.</summary>
internal static class VehicleCatalogLookup
{
    /// <summary>
    /// Returns the canonical code of the active entry matching <paramref name="value"/> (ignoring case),
    /// or null for an empty value. An inactive entry is accepted only when it is the value the vehicle
    /// already has, so deactivating an entry never blocks editing the vehicles that still use it.
    /// </summary>
    /// <param name="field">Name of the write field, used as key of the validation failure.</param>
    public static async Task<Result<string?>> ResolveAsync(
        IVehicleCatalogRepository catalog,
        VehicleCatalogKind kind,
        string? value,
        string? currentCode,
        string field,
        CancellationToken cancellationToken)
    {
        string? code = OptionalText.Normalize(value);
        if (code is null)
        {
            return Result<string?>.Success(null);
        }

        VehicleCatalogEntry? entry = await catalog.FindByCodeAsync(kind, code, cancellationToken);
        if (entry is null || (!entry.IsActive && !string.Equals(entry.Code, currentCode, StringComparison.Ordinal)))
        {
            return Error.ValidationFailures(new Dictionary<string, string[]>
            {
                [field] = [$"'{code}' is not an active {VehicleCatalogErrors.Describe(kind)}."]
            });
        }

        return entry.Code;
    }
}