using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderCharges.UpdateOrderCharge;

public sealed record UpdateOrderChargeCommand(
    Guid OrderId,
    Guid ChargeId,
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice) : ICommand<OrderChargeResponse>, IOrderChargeWriteCommand;
