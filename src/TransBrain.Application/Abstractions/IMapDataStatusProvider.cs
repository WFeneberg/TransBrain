namespace TransBrain.Application.Abstractions;

/// <summary>
/// Availability of one self-hosted map service and, when the service reports it, the timestamp
/// of the OpenStreetMap data it serves. <c>OsmDataUpdatedAt</c> is null when the service is
/// down or does not report a data date.
/// </summary>
public sealed record MapServiceStatus(bool Available, DateTimeOffset? OsmDataUpdatedAt);

/// <summary>Coverage of the extract in WGS 84 degrees, from the PBF header.</summary>
public sealed record MapExtractBoundingBox(
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude);

/// <summary>
/// The OpenStreetMap extract file both map services import. <c>Region</c> is the Geofabrik
/// region path (e.g. "europe/germany/bayern/oberbayern") when the file carries one; merged or
/// hand-built extracts fall back to the bounding box. <c>OsmDataTimestamp</c> is the data
/// cutoff of the file itself, which is newer than what the services report until they
/// re-import it.
/// </summary>
public sealed record MapExtractInfo(
    string? Region,
    DateTimeOffset? OsmDataTimestamp,
    long FileSizeBytes,
    MapExtractBoundingBox? BoundingBox);

/// <summary>
/// Probes the self-hosted map services (geocoder and route planner). A probe never throws:
/// an unreachable or still-importing service is reported as unavailable, because that is the
/// answer the maintenance page exists to show.
/// </summary>
public interface IMapDataStatusProvider
{
    Task<MapServiceStatus> GetGeocoderStatusAsync(CancellationToken cancellationToken);

    Task<MapServiceStatus> GetRoutingStatusAsync(CancellationToken cancellationToken);

    /// <summary>Null when no extract file is configured, present or readable.</summary>
    Task<MapExtractInfo?> GetExtractInfoAsync(CancellationToken cancellationToken);
}
