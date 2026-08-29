using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.ListOrders;

/// <remarks>
/// Deliberately not cached, unlike the two master-data list handlers: spec section 7 excludes
/// orders because they are too volatile for the invalidation cost.
/// </remarks>
internal sealed class ListOrdersQueryHandler(ITransportOrderRepository repository)
    : IQueryHandler<ListOrdersQuery, PagedResult<OrderResponse>>
{
    public async Task<Result<PagedResult<OrderResponse>>> Handle(
        ListOrdersQuery query,
        CancellationToken cancellationToken)
    {
        OrderStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise parse into an
            // undefined member and silently filter on it. IsDefined closes that gap.
            if (!Enum.TryParse(query.Status, ignoreCase: true, out OrderStatus parsed)
                || !Enum.IsDefined(parsed))
            {
                return Error.Validation(
                    "TransportOrder.UnknownStatus",
                    $"'{query.Status}' is not a known order status.");
            }

            status = parsed;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<TransportOrder> orders = await repository.ListAsync(
            skip, query.PageSize, status, query.PickupFrom, query.PickupTo, cancellationToken);

        int totalCount = await repository.CountAsync(
            status, query.PickupFrom, query.PickupTo, cancellationToken);

        OrderResponse[] items = orders.Select(OrderResponse.From).ToArray();

        return new PagedResult<OrderResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
