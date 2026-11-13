using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;

/// <remarks><see cref="Contacts"/> replaces the location's contact persons as a whole; omitted means none.</remarks>
public sealed record UpdateCustomerLocationCommand(
    Guid CustomerId,
    Guid LocationId,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? CompanyName = null,
    string? VatId = null,
    IReadOnlyList<CustomerLocationContactData>? Contacts = null) : ICommand<CustomerLocationResponse>;
