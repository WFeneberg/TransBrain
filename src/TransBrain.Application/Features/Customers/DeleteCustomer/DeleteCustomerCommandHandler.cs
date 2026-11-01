using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.DeleteCustomer;

internal sealed class DeleteCustomerCommandHandler(ICustomerRepository repository, ICacheService cache)
    : ICommandHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCustomerCommand command, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{command.Id}'.");
        }

        await repository.RemoveAsync(customer, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(CustomerCacheKeys.Prefix, cancellationToken);

        return Unit.Value;
    }
}
