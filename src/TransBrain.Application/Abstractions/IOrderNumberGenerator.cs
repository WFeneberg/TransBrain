using TransBrain.Domain.Orders;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// Hands out the next order number for a year. Implementations must be safe against concurrent
/// callers — two orders created at the same instant must never receive the same number.
/// </summary>
public interface IOrderNumberGenerator
{
    Task<OrderNumber> NextAsync(int year, CancellationToken cancellationToken);
}
