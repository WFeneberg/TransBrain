using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CustomerLocations.GetCustomerLocationPosition;

public sealed record GetCustomerLocationPositionQuery(Guid CustomerId, Guid LocationId)
    : IQuery<CustomerLocationPositionResponse>;
