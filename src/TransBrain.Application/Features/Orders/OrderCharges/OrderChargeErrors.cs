using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Orders.OrderCharges;

internal static class OrderChargeErrors
{
    public static Error OrderNotFound(Guid orderId) =>
        Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{orderId}'.");

    public static Error NotFound(Guid chargeId) =>
        Error.NotFound("OrderAdditionalCharge.NotFound", $"No additional charge with id '{chargeId}'.");
}
