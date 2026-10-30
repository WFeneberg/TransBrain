using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Abstractions;

public interface IRoutePlanner
{
    /// <summary>
    /// Plans a truck route through the waypoints in the given order. Needs at least two
    /// waypoints. Answers one segment per consecutive pair of waypoints.
    /// </summary>
    Task<Result<RoadRoute>> PlanAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken);
}

public sealed record RoadRoute(IReadOnlyList<RouteSegment> Segments, IReadOnlyList<GeoPoint> Geometry);

public sealed record RouteSegment(double DistanceMeters, TimeSpan Duration);
