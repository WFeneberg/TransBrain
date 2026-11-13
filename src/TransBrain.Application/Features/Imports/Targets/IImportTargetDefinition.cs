using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports.Targets;

/// <summary>
/// One entity foreign rows can be imported into: its mappable fields for the UI, and the bridge
/// that turns a converted row into the entity's regular create command. Imports deliberately go
/// through <see cref="ISender"/> so every row passes the exact validation, duplicate checks and
/// cache invalidation a manually entered record passes.
/// </summary>
internal interface IImportTargetDefinition
{
    ImportTargetEntity Entity { get; }

    /// <summary>German display name for the mapping UI, for example "Kunden".</summary>
    string Label { get; }

    IReadOnlyList<ImportTargetField> Fields { get; }

    Task<Result<Unit>> ImportRowAsync(ISender sender, ImportRowValues values, CancellationToken cancellationToken);
}

/// <summary>The fixed set of import targets.</summary>
internal static class ImportTargetRegistry
{
    public static IReadOnlyList<IImportTargetDefinition> All { get; } =
    [
        new CustomerImportTarget(),
        new DriverImportTarget(),
        new VehicleImportTarget()
    ];

    public static IImportTargetDefinition? Find(string entity) =>
        All.FirstOrDefault(t => string.Equals(t.Entity.ToString(), entity, StringComparison.OrdinalIgnoreCase));
}
