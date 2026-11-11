using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Geodata.ResolveGeodata;

/// <summary>
/// Resolves an address right now, through the same path the maps use: override, then cache, then
/// geocoder. Lets an operator check whether an address needs a manual position.
/// </summary>
public sealed record ResolveGeodataCommand(
    string Street,
    string PostalCode,
    string City,
    string Country) : ICommand<GeodataPositionResponse>;
