using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.CancelOrder;

internal sealed class CancelOrderCommandHandler(ITransportOrderRepository repository)
    : ICommandHandler<CancelOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        TransportOrder? order = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (order is null)
        {
            return Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{command.Id}'.");
        }

        Result<Unit> cancelled = order.Cancel();
        if (!cancelled.IsSuccess)
        {
            return cancelled.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OrderResponse.From(order);
    }
}
