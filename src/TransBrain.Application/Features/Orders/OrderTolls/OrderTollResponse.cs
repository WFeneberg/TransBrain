using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderTolls;

public sealed record OrderTollResponse(
    Guid Id,
    Guid OrderId,
    string TollCategory,
    int EmptyKm,
    int LoadedKm,
    decimal FactorPerKm,
    decimal EmptyAmount,
    decimal LoadedAmount,
    DateTimeOffset ModifiedAt,
    string? ModifiedBy)
{
    public static OrderTollResponse From(OrderTollCharge toll) => new(
        toll.Id,
        toll.OrderId,
        toll.TollCategory,
        toll.EmptyKm,
        toll.LoadedKm,
        toll.FactorPerKm,
        toll.EmptyAmount,
        toll.LoadedAmount,
        toll.ModifiedAt,
        toll.ModifiedBy);
}
