using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations;

public sealed record CustomerLocationResponse(
    Guid Id,
    Guid CustomerId,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone)
{
    public static CustomerLocationResponse From(CustomerLocation location) => new(
        location.Id,
        location.CustomerId,
        location.Name,
        location.Address.Street,
        location.Address.PostalCode,
        location.Address.City,
        location.Address.Country,
        location.ContactPerson,
        location.Email,
        location.Phone);
}
