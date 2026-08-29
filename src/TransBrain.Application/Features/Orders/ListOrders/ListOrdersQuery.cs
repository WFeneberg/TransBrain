using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Orders.ListOrders;

public sealed record ListOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    DateTimeOffset? PickupFrom = null,
    DateTimeOffset? PickupTo = null) : IQuery<PagedResult<OrderResponse>>;
