using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.ListFittingOrders;

/// <summary>
/// The draft orders that would still fit into the tour's remaining vehicle capacity — the
/// dispatcher's shortlist for filling up a planned route.
/// </summary>
public sealed record ListFittingOrdersQuery(Guid TourId) : IQuery<FittingOrdersResponse>;
