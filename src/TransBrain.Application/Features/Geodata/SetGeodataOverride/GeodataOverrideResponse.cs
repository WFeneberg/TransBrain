namespace TransBrain.Application.Features.Geodata.SetGeodataOverride;

public sealed record GeodataOverrideResponse(
    Guid Id,
    string Street,
    string PostalCode,
    string City,
    string Country,
    double Latitude,
    double Longitude,
    DateTimeOffset UpdatedAt);
