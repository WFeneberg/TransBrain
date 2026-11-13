using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderTolls.ListOrderTolls;

internal sealed class ListOrderTollsQueryHandler(ITransportOrderRepository repository)
    : IQueryHandler<ListOrderTollsQuery, OrderTollResponse[]>
{
    public async Task<Result<OrderTollResponse[]>> Handle(
        ListOrderTollsQuery query,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(query.OrderId, cancellationToken) is null)
        {
            return OrderTollErrors.OrderNotFound(query.OrderId);
        }

        IReadOnlyList<OrderTollCharge> tolls = await repository.ListTollsAsync(query.OrderId, cancellationToken);

        return tolls.Select(OrderTollResponse.From).ToArray();
    }
}
