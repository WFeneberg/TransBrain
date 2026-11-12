using TransBrain.Domain.Common;

namespace TransBrain.Domain.Routing;

/// <summary>A WGS 84 position in decimal degrees.</summary>
public sealed record GeoPoint
{
    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static Result<GeoPoint> Create(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            return Error.Validation("GeoPoint.LatitudeOutOfRange", "Latitude must be between -90 and 90.");
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            return Error.Validation("GeoPoint.LongitudeOutOfRange", "Longitude must be between -180 and 180.");
        }

        return new GeoPoint(latitude, longitude);
    }

    /// <summary>
    /// Great-circle distance to <paramref name="other"/> in meters, by the haversine formula.
    /// An air-line measure: good for comparing stop orders, not for billing driven kilometers —
    /// those come from the road routing service.
    /// </summary>
    public double StraightLineMetersTo(GeoPoint other)
    {
        const double earthRadiusMeters = 6_371_000;

        double latA = Latitude * Math.PI / 180;
        double latB = other.Latitude * Math.PI / 180;
        double deltaLat = (other.Latitude - Latitude) * Math.PI / 180;
        double deltaLon = (other.Longitude - Longitude) * Math.PI / 180;

        double h = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
            + Math.Cos(latA) * Math.Cos(latB) * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        return 2 * earthRadiusMeters * Math.Asin(Math.Sqrt(Math.Min(1, h)));
    }
}
