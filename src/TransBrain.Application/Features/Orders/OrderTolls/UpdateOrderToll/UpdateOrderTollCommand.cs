using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderTolls.UpdateOrderToll;

public sealed record UpdateOrderTollCommand(
    Guid OrderId,
    Guid TollId,
    string TollCategory,
    int EmptyKm,
    int LoadedKm,
    decimal FactorPerKm) : ICommand<OrderTollResponse>, IOrderTollWriteCommand;
