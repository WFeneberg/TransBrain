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

    public async Task<IReadOnlyList<TransportOrder>> ListByStatusAsync(
        OrderStatus status,
        CancellationToken cancellationToken)
        => await context.TransportOrders
            .Where(o => o.Status == status)
            .OrderBy(o => o.OrderNumber)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<OrderTollCharge>> ListTollsAsync(
        Guid orderId, CancellationToken cancellationToken)
        => await context.OrderTollCharges
            .Where(t => t.OrderId == orderId)
            .OrderBy(t => t.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<OrderTollCharge?> GetTollAsync(Guid orderId, Guid tollId, CancellationToken cancellationToken)
        => context.OrderTollCharges
            .SingleOrDefaultAsync(t => t.OrderId == orderId && t.Id == tollId, cancellationToken);

    public async Task AddTollAsync(OrderTollCharge toll, CancellationToken cancellationToken)
    {
        await context.OrderTollCharges.AddAsync(toll, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveTollAsync(OrderTollCharge toll, CancellationToken cancellationToken)
    {
        context.OrderTollCharges.Remove(toll);
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderAdditionalCharge>> ListChargesAsync(
        Guid orderId, CancellationToken cancellationToken)
        => await context.OrderAdditionalCharges
            .Where(c => c.OrderId == orderId)
            .OrderBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<OrderAdditionalCharge?> GetChargeAsync(
        Guid orderId, Guid chargeId, CancellationToken cancellationToken)
        => context.OrderAdditionalCharges
            .SingleOrDefaultAsync(c => c.OrderId == orderId && c.Id == chargeId, cancellationToken);

    public async Task AddChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken)
    {
        await context.OrderAdditionalCharges.AddAsync(charge, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken)
    {
        context.OrderAdditionalCharges.Remove(charge);
        return context.SaveChangesAsync(cancellationToken);
    }

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
