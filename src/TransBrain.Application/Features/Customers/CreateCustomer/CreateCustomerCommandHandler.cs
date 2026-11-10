using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CreateCustomer;

internal sealed class CreateCustomerCommandHandler(
    ICustomerRepository repository,
    IVehicleCatalogRepository catalog,
    ICacheService cache)
    : ICommandHandler<CreateCustomerCommand, CustomerResponse>
{
    public async Task<Result<CustomerResponse>> Handle(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        Result<Address> address = Address.Create(
            command.Name,
            command.Street,
            command.PostalCode,
            command.City,
            command.Country);

        if (!address.IsSuccess)
        {
            return address.Error!;
        }

        Result<CustomerAdditionalInfo> additional = await CustomerWriteMapping.ToAdditionalAsync(
            command.Additional, catalog, current: null, cancellationToken);
        if (!additional.IsSuccess)
        {
            return additional.Error!;
        }

        Result<Customer> customer = Customer.Create(
            command.Name,
            address.Value,
            command.ContactPerson,
            command.Email,
            command.Phone,
            command.VatId,
            command.PaymentTermDays,
            additional.Value);

        if (!customer.IsSuccess)
        {
            return customer.Error!;
        }

        await repository.AddAsync(customer.Value, cancellationToken);

        await cache.RemoveByPrefixAsync(CustomerCacheKeys.Prefix, cancellationToken);

        return CustomerResponse.From(customer.Value);
    }
}
