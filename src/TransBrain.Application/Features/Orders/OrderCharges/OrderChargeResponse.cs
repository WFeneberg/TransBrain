using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderCharges;

public sealed record OrderChargeResponse(
    Guid Id,
    Guid OrderId,
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal Total,
    DateTimeOffset ModifiedAt,
    string? ModifiedBy)
{
    public static OrderChargeResponse From(OrderAdditionalCharge charge) => new(
        charge.Id,
        charge.OrderId,
        charge.Description,
        charge.Quantity,
        charge.Unit,
        charge.UnitPrice,
        charge.Total,
        charge.ModifiedAt,
        charge.ModifiedBy);
}
