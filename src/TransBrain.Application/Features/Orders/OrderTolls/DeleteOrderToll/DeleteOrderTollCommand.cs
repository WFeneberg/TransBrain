using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Orders.OrderTolls.DeleteOrderToll;

public sealed record DeleteOrderTollCommand(Guid OrderId, Guid TollId) : ICommand<Unit>;
