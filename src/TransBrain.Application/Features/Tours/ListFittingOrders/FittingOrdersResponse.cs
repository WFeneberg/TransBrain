using TransBrain.Application.Features.Orders;

namespace TransBrain.Application.Features.Tours.ListFittingOrders;

/// <summary>
/// The capacity a tour still has and every draft order that fits into it. The remaining numbers
/// are carried alongside the orders so the UI can say "3,2 t / 4,5 ldm frei" without re-deriving
/// the tour's sums.
/// </summary>
public sealed record FittingOrdersResponse(
    Guid TourId,
    int RemainingWeightKg,
    decimal RemainingLoadMeters,
    IReadOnlyList<OrderResponse> Orders);
