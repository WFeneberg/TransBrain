namespace TransBrain.Application.Features.Orders;

/// <summary>A party on an order, placed on the map.</summary>
public sealed record PlaceResponse(string Name, string Address, double Latitude, double Longitude);

/// <summary>
/// The consignor and consignee of an order as map positions. A party whose address matches no
/// known place is null, so the other party still shows.
/// </summary>
public sealed record OrderLocationsResponse(PlaceResponse? Consignor, PlaceResponse? Consignee);
