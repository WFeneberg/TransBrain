namespace TransBrain.Application.Abstractions;

/// <summary>
/// Availability of one self-hosted map service and, when the service reports it, the timestamp
/// of the OpenStreetMap data it serves. <c>OsmDataUpdatedAt</c> is null when the service is
/// down or does not report a data date.
/// </summary>
public sealed record MapServiceStatus(bool Available, DateTimeOffset? OsmDataUpdatedAt);

/// <summary>
/// Probes the self-hosted map services (geocoder and route planner). A probe never throws:
/// an unreachable or still-importing service is reported as unavailable, because that is the
/// answer the maintenance page exists to show.
/// </summary>
public interface IMapDataStatusProvider
{
    Task<MapServiceStatus> GetGeocoderStatusAsync(CancellationToken cancellationToken);

    Task<MapServiceStatus> GetRoutingStatusAsync(CancellationToken cancellationToken);
}
