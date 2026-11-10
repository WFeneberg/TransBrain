using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? VatId,
    int PaymentTermDays)
{
    public CustomerAdditionalData Additional { get; init; } = new();

    public static CustomerResponse From(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Address.Street,
        customer.Address.PostalCode,
        customer.Address.City,
        customer.Address.Country,
        customer.ContactPerson,
        customer.Email,
        customer.Phone,
        customer.VatId,
        customer.PaymentTermDays)
    {
        Additional = CustomerAdditionalData.From(customer.Additional),
    };
}
