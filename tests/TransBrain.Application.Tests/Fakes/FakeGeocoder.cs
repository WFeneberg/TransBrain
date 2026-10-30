using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Answers from a fixed set of cities. Any other address is NotFound, as the real service does.</summary>
public sealed class FakeGeocoder : IGeocoder
{
    private readonly Dictionary<string, GeoPoint> _byCity = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Set to make every call answer this error, for example an unconfigured or unreachable service.</summary>
    public Error? Failure { get; set; }

    public FakeGeocoder Knows(string city, double latitude, double longitude)
    {
        _byCity[city] = GeoPoint.Create(latitude, longitude).Value;
        return this;
    }

    public Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(Result<GeoPoint>.Failure(Failure));
        }

        Result<GeoPoint> result = _byCity.TryGetValue(address.City, out GeoPoint? point)
            ? point
            : Error.NotFound("Geocoding.NoMatch", $"No place found for '{address.City}'.");

        return Task.FromResult(result);
    }
}
