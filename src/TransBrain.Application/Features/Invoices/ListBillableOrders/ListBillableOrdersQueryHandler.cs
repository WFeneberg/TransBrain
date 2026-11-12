using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Orders;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Invoices.ListBillableOrders;

internal sealed class ListBillableOrdersQueryHandler(IInvoiceRepository repository)
    : IQueryHandler<ListBillableOrdersQuery, IReadOnlyList<OrderResponse>>
{
    public async Task<Result<IReadOnlyList<OrderResponse>>> Handle(
        ListBillableOrdersQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TransportOrder> orders = await repository.ListBillableOrdersAsync(cancellationToken);

        return Result<IReadOnlyList<OrderResponse>>.Success(
            orders.Select(OrderResponse.From).ToArray());
    }
}
