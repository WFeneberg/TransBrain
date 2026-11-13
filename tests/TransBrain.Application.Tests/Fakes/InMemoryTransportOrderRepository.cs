using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryTransportOrderRepository : ITransportOrderRepository
{
    private readonly List<TransportOrder> _orders = [];

    public IReadOnlyList<TransportOrder> Orders => _orders;

    public int SaveChangesCallCount { get; private set; }

    public void Seed(params TransportOrder[] orders) => _orders.AddRange(orders);

    public Task<Result<TransportOrder>> AddAsync(TransportOrder order, CancellationToken cancellationToken)
    {
        _orders.Add(order);
        return Task.FromResult(Result<TransportOrder>.Success(order));
    }

    public Task<TransportOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_orders.SingleOrDefault(o => o.Id == id));

    // Ordinal ordering on the order number, matching the EF repository's column collation.
    // The fake must not define a different notion of "sorted" from the one production uses.
    public Task<IReadOnlyList<TransportOrder>> ListAsync(
        int skip,
        int take,
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TransportOrder>>(
            Filter(status, pickupFrom, pickupTo)
                .OrderBy(o => o.OrderNumber.Value, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken)
        => Task.FromResult(Filter(status, pickupFrom, pickupTo).Count());

    public Task<IReadOnlyList<TransportOrder>> ListByStatusAsync(
        OrderStatus status,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TransportOrder>>(
            _orders
                .Where(o => o.Status == status)
                .OrderBy(o => o.OrderNumber.Value, StringComparer.Ordinal)
                .ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    private readonly List<OrderTollCharge> _tolls = [];

    private readonly List<OrderAdditionalCharge> _charges = [];

    public IReadOnlyList<OrderTollCharge> Tolls => _tolls;

    public IReadOnlyList<OrderAdditionalCharge> Charges => _charges;

    public void SeedTolls(params OrderTollCharge[] tolls) => _tolls.AddRange(tolls);

    public void SeedCharges(params OrderAdditionalCharge[] charges) => _charges.AddRange(charges);

    // Insertion order stands in for the EF repository's "oldest first" (version-7 ids ascend).
    public Task<IReadOnlyList<OrderTollCharge>> ListTollsAsync(Guid orderId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OrderTollCharge>>(_tolls.Where(t => t.OrderId == orderId).ToList());

    public Task<OrderTollCharge?> GetTollAsync(Guid orderId, Guid tollId, CancellationToken cancellationToken)
        => Task.FromResult(_tolls.SingleOrDefault(t => t.OrderId == orderId && t.Id == tollId));

    public Task AddTollAsync(OrderTollCharge toll, CancellationToken cancellationToken)
    {
        _tolls.Add(toll);
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveTollAsync(OrderTollCharge toll, CancellationToken cancellationToken)
    {
        _tolls.Remove(toll);
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OrderAdditionalCharge>> ListChargesAsync(
        Guid orderId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OrderAdditionalCharge>>(
            _charges.Where(c => c.OrderId == orderId).ToList());

    public Task<OrderAdditionalCharge?> GetChargeAsync(
        Guid orderId, Guid chargeId, CancellationToken cancellationToken)
        => Task.FromResult(_charges.SingleOrDefault(c => c.OrderId == orderId && c.Id == chargeId));

    public Task AddChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken)
    {
        _charges.Add(charge);
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task RemoveChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken)
    {
        _charges.Remove(charge);
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    private IEnumerable<TransportOrder> Filter(
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo)
    {
        IEnumerable<TransportOrder> query = _orders;

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
