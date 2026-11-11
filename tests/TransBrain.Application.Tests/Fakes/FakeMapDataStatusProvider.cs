using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

internal sealed class FakeMapDataStatusProvider : IMapDataStatusProvider
{
    public MapServiceStatus Geocoder { get; set; } = new(Available: false, OsmDataUpdatedAt: null);

    public MapServiceStatus Routing { get; set; } = new(Available: false, OsmDataUpdatedAt: null);

    public MapExtractInfo? Extract { get; set; }

    public Task<MapServiceStatus> GetGeocoderStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(Geocoder);

    public Task<MapServiceStatus> GetRoutingStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(Routing);

    public Task<MapExtractInfo?> GetExtractInfoAsync(CancellationToken cancellationToken)
        => Task.FromResult(Extract);
}
