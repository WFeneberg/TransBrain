using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Abstractions;

public interface IGeocoder
{
    /// <summary>
    /// Resolves a postal address to a position. Answers NotFound when no match exists, and
    /// Unavailable when the geocoding service cannot be reached or is not configured.
    /// </summary>
    Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken cancellationToken);
}
