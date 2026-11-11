namespace TransBrain.Application.Features.Geodata;

/// <summary>One distinct address the system routes to, with how often it is used.</summary>
public sealed record UsedAddress(
    string Street,
    string PostalCode,
    string City,
    string Country,
    int OrderCount,
    int LocationCount);
