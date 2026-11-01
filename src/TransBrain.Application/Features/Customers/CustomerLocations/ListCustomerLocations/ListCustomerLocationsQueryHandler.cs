using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Customers.CustomerLocations.ListCustomerLocations;

internal sealed class ListCustomerLocationsQueryHandler(ICustomerRepository repository)
    : IQueryHandler<ListCustomerLocationsQuery, CustomerLocationResponse[]>
{
    public async Task<Result<CustomerLocationResponse[]>> Handle(
        ListCustomerLocationsQuery query,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(query.CustomerId, cancellationToken) is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{query.CustomerId}'.");
        }

        IReadOnlyList<Domain.Customers.CustomerLocation> locations =
            await repository.ListLocationsAsync(query.CustomerId, cancellationToken);

        return locations.Select(CustomerLocationResponse.From).ToArray();
    }
}
