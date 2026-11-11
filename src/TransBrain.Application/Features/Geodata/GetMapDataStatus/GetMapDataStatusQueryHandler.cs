using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Geodata.GetMapDataStatus;

/// <remarks>
/// An unavailable service is a successful answer here, not an error: the page exists to show
/// exactly that state. The probes run in parallel because they are independent.
/// </remarks>
internal sealed class GetMapDataStatusQueryHandler(IMapDataStatusProvider provider)
    : IQueryHandler<GetMapDataStatusQuery, MapDataStatusResponse>
{
    public async Task<Result<MapDataStatusResponse>> Handle(
        GetMapDataStatusQuery query,
        CancellationToken cancellationToken)
    {
        Task<MapServiceStatus> geocoder = provider.GetGeocoderStatusAsync(cancellationToken);
        Task<MapServiceStatus> routing = provider.GetRoutingStatusAsync(cancellationToken);
        Task<MapExtractInfo?> extract = provider.GetExtractInfoAsync(cancellationToken);
        await Task.WhenAll(geocoder, routing, extract);

        return new MapDataStatusResponse(
            new MapServiceStatusResponse(geocoder.Result.Available, geocoder.Result.OsmDataUpdatedAt),
            new MapServiceStatusResponse(routing.Result.Available, routing.Result.OsmDataUpdatedAt),
            ToResponse(extract.Result));
    }

    private static MapExtractResponse? ToResponse(MapExtractInfo? extract)
        => extract is null
            ? null
            : new MapExtractResponse(
                extract.Region,
                extract.OsmDataTimestamp,
                extract.FileSizeBytes,
                extract.BoundingBox is { } box
                    ? new MapExtractBoundingBoxResponse(
                        box.MinLatitude, box.MinLongitude, box.MaxLatitude, box.MaxLongitude)
                    : null);
}
