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

    /// <summary>The order's toll records ("Mautdaten"), oldest first.</summary>
    Task<IReadOnlyList<OrderTollCharge>> ListTollsAsync(Guid orderId, CancellationToken cancellationToken);

    Task<OrderTollCharge?> GetTollAsync(Guid orderId, Guid tollId, CancellationToken cancellationToken);

    /// <summary>Adds and saves.</summary>
    Task AddTollAsync(OrderTollCharge toll, CancellationToken cancellationToken);

    /// <summary>Removes and saves.</summary>
    Task RemoveTollAsync(OrderTollCharge toll, CancellationToken cancellationToken);

    /// <summary>The order's additional charge positions ("Zusatzpositionen"), oldest first.</summary>
    Task<IReadOnlyList<OrderAdditionalCharge>> ListChargesAsync(Guid orderId, CancellationToken cancellationToken);

    Task<OrderAdditionalCharge?> GetChargeAsync(Guid orderId, Guid chargeId, CancellationToken cancellationToken);

    /// <summary>Adds and saves.</summary>
    Task AddChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken);

    /// <summary>Removes and saves.</summary>
    Task RemoveChargeAsync(OrderAdditionalCharge charge, CancellationToken cancellationToken);
}
