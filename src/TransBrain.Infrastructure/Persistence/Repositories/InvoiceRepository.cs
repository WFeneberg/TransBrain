using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository(TransBrainDbContext context) : IInvoiceRepository
{
    public async Task<Result<Invoice>> AddAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        await context.Invoices.AddAsync(invoice, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    // Lines are an owned collection, so they load with the invoice; no Include is needed. They
    // are tracked deliberately — the status transitions mutate the aggregate.
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Invoices.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> ListAsync(
        int skip,
        int take,
        InvoiceStatus? status,
        Guid? customerId,
        CancellationToken cancellationToken)
        => await Filter(status, customerId)
            .OrderByDescending(i => i.InvoiceNumber)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(InvoiceStatus? status, Guid? customerId, CancellationToken cancellationToken)
        => Filter(status, customerId).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetBilledOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
        => await BilledOrderIds()
            .Where(orderId => orderIds.Contains(orderId))
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TransportOrder>> ListBillableOrdersAsync(CancellationToken cancellationToken)
        => await context.TransportOrders
            .Where(order => order.Status == OrderStatus.Delivered)
            .Where(order => !BilledOrderIds().Contains(order.Id))
            .OrderBy(order => order.OrderNumber)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // "Billed" means: on a line of any invoice that is not cancelled. Cancelled invoices
    // release their orders for re-billing.
    private IQueryable<Guid> BilledOrderIds()
        => context.Invoices
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .SelectMany(invoice => invoice.Lines.Select(line => line.TransportOrderId));

    private IQueryable<Invoice> Filter(InvoiceStatus? status, Guid? customerId)
    {
        IQueryable<Invoice> query = context.Invoices;

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
