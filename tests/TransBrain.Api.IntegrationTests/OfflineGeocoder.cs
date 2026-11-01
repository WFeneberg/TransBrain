using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Api.IntegrationTests;

/// <summary>Answers every lookup as unavailable, so integration tests never reach an external geocoding service.</summary>
internal sealed class OfflineGeocoder : IGeocoder
{
    public Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken cancellationToken) =>
        Task.FromResult(Result<GeoPoint>.Failure(
            Error.Unavailable("Geocoding.Offline", "Geocoding is disabled in integration tests.")));
}
