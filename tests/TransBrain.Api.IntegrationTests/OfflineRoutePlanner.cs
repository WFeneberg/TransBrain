using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Api.IntegrationTests;

/// <summary>Answers every route as unavailable, so integration tests never depend on a routing container.</summary>
internal sealed class OfflineRoutePlanner : IRoutePlanner
{
    public Task<Result<RoadRoute>> PlanAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken) =>
        Task.FromResult(Result<RoadRoute>.Failure(
            Error.Unavailable("Routing.Offline", "Routing is disabled in integration tests.")));
}
