using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Geodata.SetGeodataOverride;

/// <summary>Creates or replaces the manual position for the given address.</summary>
public sealed record SetGeodataOverrideCommand(
    string Street,
    string PostalCode,
    string City,
    string Country,
    double Latitude,
    double Longitude) : ICommand<GeodataOverrideResponse>;
