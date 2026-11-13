using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderCharges.AddOrderCharge;

public sealed record AddOrderChargeCommand(
    Guid OrderId,
    string Description,
    decimal Quantity,
    string? Unit = null,
    decimal UnitPrice = 0m) : ICommand<OrderChargeResponse>, IOrderChargeWriteCommand;
