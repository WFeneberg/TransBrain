using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class TransportOrderRepository(TransBrainDbContext context) : ITransportOrderRepository
{
    public async Task<Result<TransportOrder>> AddAsync(TransportOrder order, CancellationToken cancellationToken)
    {
        await context.TransportOrders.AddAsync(order, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return order;
    }

    public Task<TransportOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.TransportOrders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TransportOrder>> ListAsync(
        int skip,
        int take,
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken)
        => await Filter(status, pickupFrom, pickupTo)
            .OrderBy(o => o.OrderNumber)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken)
        => Filter(status, pickupFrom, pickupTo).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    private IQueryable<TransportOrder> Filter(
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo)
    {
        IQueryable<TransportOrder> query = context.TransportOrders;

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (pickupFrom is not null)
        {
            query = query.Where(o => o.PickupWindow.From >= pickupFrom);
        }

        if (pickupTo is not null)
        {
            query = query.Where(o => o.PickupWindow.From <= pickupTo);
        }

        return query;
    }
}
