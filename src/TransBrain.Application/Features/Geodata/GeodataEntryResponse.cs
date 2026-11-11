namespace TransBrain.Application.Features.Geodata;

/// <summary>Where a shown position comes from, or that there is none.</summary>
public static class GeodataSources
{
    public const string Manual = "manual";

    public const string Geocoded = "geocoded";

    public const string Unresolved = "unresolved";
}

/// <summary>
/// One address in the geodata overview. <c>Source</c> is one of <see cref="GeodataSources"/>;
/// coordinates are absent when the address is unresolved. <c>InUse</c> is false for a manual
/// override whose address no longer appears in any order or delivery location.
/// </summary>
public sealed record GeodataEntryResponse(
    string Street,
    string PostalCode,
    string City,
    string Country,
    double? Latitude,
    double? Longitude,
    string Source,
    Guid? OverrideId,
    int OrderCount,
    int LocationCount,
    bool InUse,
    DateTimeOffset? UpdatedAt);
