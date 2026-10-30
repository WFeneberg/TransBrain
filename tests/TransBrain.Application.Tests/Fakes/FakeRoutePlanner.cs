using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Every segment is 100 km and takes <see cref="SegmentDuration"/>, unless <see cref="Failure"/> is set.</summary>
public sealed class FakeRoutePlanner : IRoutePlanner
{
    public TimeSpan SegmentDuration { get; set; } = TimeSpan.FromHours(2);

    public Error? Failure { get; set; }

    public int CallCount { get; private set; }

    public Task<Result<RoadRoute>> PlanAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken)
    {
        CallCount++;

        if (Failure is not null)
        {
            return Task.FromResult(Result<RoadRoute>.Failure(Failure));
        }

        RouteSegment[] segments = Enumerable.Range(0, waypoints.Count - 1)
            .Select(_ => new RouteSegment(100_000, SegmentDuration))
            .ToArray();

        return Task.FromResult(Result<RoadRoute>.Success(new RoadRoute(segments, waypoints)));
    }
}
