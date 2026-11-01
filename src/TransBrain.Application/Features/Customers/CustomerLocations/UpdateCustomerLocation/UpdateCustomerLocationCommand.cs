using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;

public sealed record UpdateCustomerLocationCommand(
    Guid CustomerId,
    Guid LocationId,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone) : ICommand<CustomerLocationResponse>;
