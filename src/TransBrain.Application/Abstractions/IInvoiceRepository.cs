using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Abstractions;

public interface IInvoiceRepository
{
    Task<Result<Invoice>> AddAsync(Invoice invoice, CancellationToken cancellationToken);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Invoice>> ListAsync(
        int skip,
        int take,
        InvoiceStatus? status,
        Guid? customerId,
        CancellationToken cancellationToken);

    Task<int> CountAsync(InvoiceStatus? status, Guid? customerId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The subset of <paramref name="orderIds"/> that already appear on a line of a
    /// non-cancelled invoice. Cancelled invoices do not count: cancelling releases the orders.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetBilledOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken);

    /// <summary>Delivered orders that are not yet on any non-cancelled invoice, i.e. still billable.</summary>
    Task<IReadOnlyList<TransportOrder>> ListBillableOrdersAsync(CancellationToken cancellationToken);
}
