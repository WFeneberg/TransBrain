using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand<Unit>;
