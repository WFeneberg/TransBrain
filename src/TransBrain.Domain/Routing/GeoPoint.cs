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
}
