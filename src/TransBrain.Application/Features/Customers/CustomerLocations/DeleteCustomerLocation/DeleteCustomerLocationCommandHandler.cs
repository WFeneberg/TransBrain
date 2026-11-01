using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations.DeleteCustomerLocation;

internal sealed class DeleteCustomerLocationCommandHandler(ICustomerRepository repository)
    : ICommandHandler<DeleteCustomerLocationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCustomerLocationCommand command, CancellationToken cancellationToken)
    {
        CustomerLocation? location = await repository.GetLocationAsync(
            command.CustomerId, command.LocationId, cancellationToken);
        if (location is null)
        {
            return Error.NotFound("CustomerLocation.NotFound", $"No delivery location with id '{command.LocationId}'.");
        }

        await repository.RemoveLocationAsync(location, cancellationToken);

        return Unit.Value;
    }
}
