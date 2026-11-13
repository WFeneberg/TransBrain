using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderCharges.ListOrderCharges;

internal sealed class ListOrderChargesQueryHandler(ITransportOrderRepository repository)
    : IQueryHandler<ListOrderChargesQuery, OrderChargeResponse[]>
{
    public async Task<Result<OrderChargeResponse[]>> Handle(
        ListOrderChargesQuery query,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(query.OrderId, cancellationToken) is null)
        {
            return OrderChargeErrors.OrderNotFound(query.OrderId);
        }

        IReadOnlyList<OrderAdditionalCharge> charges =
            await repository.ListChargesAsync(query.OrderId, cancellationToken);

        return charges.Select(OrderChargeResponse.From).ToArray();
    }
}
