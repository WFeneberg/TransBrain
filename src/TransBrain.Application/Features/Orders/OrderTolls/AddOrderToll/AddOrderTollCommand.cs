using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderTolls.AddOrderToll;

public sealed record AddOrderTollCommand(
    Guid OrderId,
    string TollCategory,
    int EmptyKm = 0,
    int LoadedKm = 0,
    decimal FactorPerKm = 0m) : ICommand<OrderTollResponse>, IOrderTollWriteCommand;
