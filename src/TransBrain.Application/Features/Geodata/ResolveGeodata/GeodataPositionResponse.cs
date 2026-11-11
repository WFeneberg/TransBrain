namespace TransBrain.Application.Features.Geodata.ResolveGeodata;

/// <summary><c>Source</c> is one of <see cref="GeodataSources"/>.</summary>
public sealed record GeodataPositionResponse(double Latitude, double Longitude, string Source);
