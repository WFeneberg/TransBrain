using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Orders.OrderTolls;

internal static class OrderTollErrors
{
    public static Error OrderNotFound(Guid orderId) =>
        Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{orderId}'.");

    public static Error NotFound(Guid tollId) =>
        Error.NotFound("OrderTollCharge.NotFound", $"No toll record with id '{tollId}'.");
}
