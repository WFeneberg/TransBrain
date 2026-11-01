using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Customers.CustomerLocations.DeleteCustomerLocation;

public sealed record DeleteCustomerLocationCommand(Guid CustomerId, Guid LocationId) : ICommand<Unit>;
