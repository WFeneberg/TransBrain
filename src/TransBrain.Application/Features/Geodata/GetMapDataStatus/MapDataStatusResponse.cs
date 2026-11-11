namespace TransBrain.Application.Features.Geodata.GetMapDataStatus;

/// <summary>One self-hosted map service as shown on the maintenance page.</summary>
public sealed record MapServiceStatusResponse(bool Available, DateTimeOffset? OsmDataUpdatedAt);

/// <summary>Coverage of the extract file in WGS 84 degrees.</summary>
public sealed record MapExtractBoundingBoxResponse(
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude);

/// <summary>
/// The OSM extract file installed on the server: the Geofabrik region it was downloaded as
/// (null for merged or hand-built files, which at best carry a bounding box), the data cutoff
/// of the file and its size. The file can be newer than what the services report — then it was
/// replaced but not yet re-imported.
/// </summary>
public sealed record MapExtractResponse(
    string? Region,
    DateTimeOffset? OsmDataTimestamp,
    long FileSizeBytes,
    MapExtractBoundingBoxResponse? BoundingBox);

/// <summary>
/// State of the map material behind the geodata page: the geocoder (Nominatim) and the route
/// planner (OpenRouteService), each with the OSM data date it reports, plus the extract file
/// itself when the server exposes it. The map extract is updated on the server, not through
/// the API — this response only makes its state visible.
/// </summary>
public sealed record MapDataStatusResponse(
    MapServiceStatusResponse Geocoder,
    MapServiceStatusResponse Routing,
    MapExtractResponse? Extract);
