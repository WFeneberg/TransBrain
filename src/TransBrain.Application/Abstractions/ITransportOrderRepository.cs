using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Abstractions;

public interface ITransportOrderRepository
{
    Task<Result<TransportOrder>> AddAsync(TransportOrder order, CancellationToken cancellationToken);

    Task<TransportOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransportOrder>> ListAsync(
        int skip,
        int take,
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken);

    /// <summary>All orders in the given status, sorted by order number. Unpaged: used by planning
    /// aids that must see every candidate, not one page of them.</summary>
    Task<IReadOnlyList<TransportOrder>> ListByStatusAsync(OrderStatus status, CancellationToken cancellationToken);

    Task<int> CountAsync(
        OrderStatus? status,
        DateTimeOffset? pickupFrom,
        DateTimeOffset? pickupTo,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
