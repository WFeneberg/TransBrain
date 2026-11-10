using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.UpdateCustomer;

internal sealed class UpdateCustomerCommandHandler(
    ICustomerRepository repository,
    IVehicleCatalogRepository catalog,
    ICacheService cache)
    : ICommandHandler<UpdateCustomerCommand, CustomerResponse>
{
    public async Task<Result<CustomerResponse>> Handle(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{command.Id}'.");
        }

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
            command.Additional, catalog, customer.Additional, cancellationToken);
        if (!additional.IsSuccess)
        {
            return additional.Error!;
        }

        Result<Customer> updated = customer.Update(
            command.Name,
            address.Value,
            command.ContactPerson,
            command.Email,
            command.Phone,
            command.VatId,
            command.PaymentTermDays,
            additional.Value);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(CustomerCacheKeys.Prefix, cancellationToken);

        return CustomerResponse.From(updated.Value);
    }
}
