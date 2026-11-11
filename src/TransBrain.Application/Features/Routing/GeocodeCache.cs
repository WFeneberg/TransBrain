using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Routing;

/// <summary>
/// Key format and stored shape of the geocode cache, shared between the resolver that fills it
/// and the geodata overview that inspects it.
/// </summary>
internal static class GeocodeCache
{
    public static string Key(Address address) => Key(GeoOverride.BuildKey(address));

    public static string Key(string street, string postalCode, string city, string country)
        => Key(GeoOverride.BuildKey(street, postalCode, city, country));

    private static string Key(string addressKey) => $"geocode:{addressKey}";

    // GeoPoint keeps its constructor private, so the cache stores this plain shape instead.
    internal sealed record CachedPoint(double Latitude, double Longitude);
}
