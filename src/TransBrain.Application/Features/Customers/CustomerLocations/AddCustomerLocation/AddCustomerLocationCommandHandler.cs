using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;

internal sealed class AddCustomerLocationCommandHandler(ICustomerRepository repository)
    : ICommandHandler<AddCustomerLocationCommand, CustomerLocationResponse>
{
    public async Task<Result<CustomerLocationResponse>> Handle(
        AddCustomerLocationCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(command.CustomerId, cancellationToken) is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{command.CustomerId}'.");
        }

        Result<Address> address = Address.Create(
            command.Name, command.Street, command.PostalCode, command.City, command.Country);
        if (!address.IsSuccess)
        {
            return address.Error!;
        }

        Result<CustomerLocation> location = CustomerLocation.Create(
            command.CustomerId, command.Name, address.Value, command.ContactPerson, command.Email, command.Phone);
        if (!location.IsSuccess)
        {
            return location.Error!;
        }

        await repository.AddLocationAsync(location.Value, cancellationToken);

        return CustomerLocationResponse.From(location.Value);
    }
}
