using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.GetCustomerById;

internal sealed class GetCustomerByIdQueryHandler(ICustomerRepository repository)
    : IQueryHandler<GetCustomerByIdQuery, CustomerResponse>
{
    public async Task<Result<CustomerResponse>> Handle(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        Customer? customer = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{query.Id}'.");
        }

        return CustomerResponse.From(customer);
    }
}
