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

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
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
