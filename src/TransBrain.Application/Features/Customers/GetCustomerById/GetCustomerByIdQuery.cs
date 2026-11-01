using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerResponse>;
