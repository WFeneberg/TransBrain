namespace TransBrain.Application.Features.Customers.CustomerLocations.GetCustomerLocationPosition;

/// <summary>Where a delivery location lies on the map, resolved from its address.</summary>
public sealed record CustomerLocationPositionResponse(
    string Name,
    string Address,
    double Latitude,
    double Longitude);
