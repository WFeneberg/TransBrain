using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderTolls.ListOrderTolls;

/// <summary>The order's toll records ("Mautdaten"), oldest first.</summary>
public sealed record ListOrderTollsQuery(Guid OrderId) : IQuery<OrderTollResponse[]>;
