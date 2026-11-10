using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? VatId,
    int PaymentTermDays,
    CustomerAdditionalData? Additional = null) : ICommand<CustomerResponse>;
