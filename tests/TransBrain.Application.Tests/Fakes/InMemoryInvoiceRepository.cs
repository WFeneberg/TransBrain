using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = [];
    private readonly List<TransportOrder> _orders = [];

    public IReadOnlyList<Invoice> Invoices => _invoices;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(params Invoice[] invoices) => _invoices.AddRange(invoices);

    /// <summary>The order pool the billable-orders query filters, standing in for transport_orders.</summary>
    public void SeedOrders(params TransportOrder[] orders) => _orders.AddRange(orders);

    public Task<Result<Invoice>> AddAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        _invoices.Add(invoice);
        return Task.FromResult(Result<Invoice>.Success(invoice));
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_invoices.SingleOrDefault(i => i.Id == id));

    // Descending ordinal ordering on the invoice number, matching the EF repository.
    public Task<IReadOnlyList<Invoice>> ListAsync(
        int skip,
        int take,
        InvoiceStatus? status,
        Guid? customerId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Invoice>>(
            Filter(status, customerId)
                .OrderByDescending(i => i.InvoiceNumber.Value, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(InvoiceStatus? status, Guid? customerId, CancellationToken cancellationToken)
        => Task.FromResult(Filter(status, customerId).Count());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetBilledOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Guid>>(
            BilledOrderIds().Where(orderIds.Contains).Distinct().ToList());

    public Task<IReadOnlyList<TransportOrder>> ListBillableOrdersAsync(CancellationToken cancellationToken)
    {
        HashSet<Guid> billed = BilledOrderIds().ToHashSet();

        return Task.FromResult<IReadOnlyList<TransportOrder>>(
            _orders
                .Where(o => o.Status == OrderStatus.Delivered && !billed.Contains(o.Id))
                .OrderBy(o => o.OrderNumber.Value, StringComparer.Ordinal)
                .ToList());
    }

    private IEnumerable<Guid> BilledOrderIds()
        => _invoices
            .Where(i => i.Status != InvoiceStatus.Cancelled)
            .SelectMany(i => i.Lines.Select(line => line.TransportOrderId));

    private IEnumerable<Invoice> Filter(InvoiceStatus? status, Guid? customerId)
    {
        IEnumerable<Invoice> query = _invoices;

        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (customerId is not null)
        {
            query = query.Where(i => i.CustomerId == customerId);
        }

        return query;
    }
}
