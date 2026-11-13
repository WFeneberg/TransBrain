namespace TransBrain.Application.Features.Imports;

/// <summary>The entities foreign data can be imported into.</summary>
public enum ImportTargetEntity
{
    Customers,
    Drivers,
    Vehicles
}

/// <summary>How a mapped cell value is typed before it reaches the target entity's create command.</summary>
public enum ImportFieldKind
{
    Text,
    Integer,
    Decimal,
    Date,
    Boolean,

    /// <summary>A list of texts in one cell, separated by <c>;</c> or <c>,</c> — for example licence classes.</summary>
    TextList
}

/// <summary>One mappable field of an import target, described for the mapping UI.</summary>
public sealed record ImportTargetField(
    string Name,
    string Label,
    ImportFieldKind Kind,
    bool Required,
    string? Description = null,
    IReadOnlyList<string>? AllowedValues = null);

/// <summary>
/// One edge of the user-drawn mapping: which source column (and/or constant) fills a target
/// field. When both are set, the constant acts as the fallback for empty source cells — this is
/// how "Land ist bei uns immer DE" imports work without touching the source data.
/// </summary>
public sealed record ImportFieldMapping(string TargetField, string? SourceColumn, string? ConstantValue);
