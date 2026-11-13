using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Orders.OrderCharges.DeleteOrderCharge;

public sealed record DeleteOrderChargeCommand(Guid OrderId, Guid ChargeId) : ICommand<Unit>;
