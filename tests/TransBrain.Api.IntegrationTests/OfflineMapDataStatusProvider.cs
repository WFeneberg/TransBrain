using TransBrain.Application.Abstractions;

namespace TransBrain.Api.IntegrationTests;

/// <summary>Reports both map services as down, so integration tests never probe real containers.</summary>
internal sealed class OfflineMapDataStatusProvider : IMapDataStatusProvider
{
    public Task<MapServiceStatus> GetGeocoderStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(new MapServiceStatus(Available: false, OsmDataUpdatedAt: null));

    public Task<MapServiceStatus> GetRoutingStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(new MapServiceStatus(Available: false, OsmDataUpdatedAt: null));
}
