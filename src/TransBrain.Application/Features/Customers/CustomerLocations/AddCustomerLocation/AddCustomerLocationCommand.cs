using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;

public sealed record AddCustomerLocationCommand(
    Guid CustomerId,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone) : ICommand<CustomerLocationResponse>;
