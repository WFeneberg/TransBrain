namespace TransBrain.Application.Features.Geodata.GetMapDataStatus;

/// <summary>One self-hosted map service as shown on the maintenance page.</summary>
public sealed record MapServiceStatusResponse(bool Available, DateTimeOffset? OsmDataUpdatedAt);

/// <summary>
/// State of the map material behind the geodata page: the geocoder (Nominatim) and the route
/// planner (OpenRouteService), each with the OSM data date it reports. The map extract itself
/// is updated on the server, not through the API — this response only makes its state visible.
/// </summary>
public sealed record MapDataStatusResponse(
    MapServiceStatusResponse Geocoder,
    MapServiceStatusResponse Routing);
