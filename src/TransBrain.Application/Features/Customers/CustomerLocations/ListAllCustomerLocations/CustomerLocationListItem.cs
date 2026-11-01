namespace TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;

/// <summary>A delivery location with its customer's name, for pickers that list locations across customers.</summary>
public sealed record CustomerLocationListItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone);
