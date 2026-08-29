using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.GetOrderById;

internal sealed class GetOrderByIdQueryHandler(ITransportOrderRepository repository)
    : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        TransportOrder? order = await repository.GetByIdAsync(query.Id, cancellationToken);

        if (order is null)
        {
            return Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{query.Id}'.");
        }

        return OrderResponse.From(order);
    }
}
