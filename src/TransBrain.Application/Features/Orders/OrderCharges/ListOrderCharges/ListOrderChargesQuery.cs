using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.OrderCharges.ListOrderCharges;

/// <summary>The order's additional charge positions ("Zusatzpositionen"), oldest first.</summary>
public sealed record ListOrderChargesQuery(Guid OrderId) : IQuery<OrderChargeResponse[]>;
