using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;

internal sealed class UpdateCustomerLocationCommandHandler(ICustomerRepository repository)
    : ICommandHandler<UpdateCustomerLocationCommand, CustomerLocationResponse>
{
    public async Task<Result<CustomerLocationResponse>> Handle(
        UpdateCustomerLocationCommand command,
        CancellationToken cancellationToken)
    {
        CustomerLocation? location = await repository.GetLocationAsync(
            command.CustomerId, command.LocationId, cancellationToken);
        if (location is null)
        {
            return Error.NotFound("CustomerLocation.NotFound", $"No delivery location with id '{command.LocationId}'.");
        }

        Result<Address> address = Address.Create(
            command.Name, command.Street, command.PostalCode, command.City, command.Country);
        if (!address.IsSuccess)
        {
            return address.Error!;
        }

        Result<List<CustomerLocationContact>> contacts = CustomerLocationContactData.ToDomain(command.Contacts);
        if (!contacts.IsSuccess)
        {
            return contacts.Error!;
        }

        Result<CustomerLocation> updated = location.Update(command.Name, address.Value, contacts.Value);
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return CustomerLocationResponse.From(updated.Value);
    }
}
