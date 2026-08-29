using TransBrain.Application.Abstractions;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Fakes;

public sealed class StubOrderNumberGenerator : IOrderNumberGenerator
{
    private int _next;

    public StubOrderNumberGenerator(int firstSequence = 1) => _next = firstSequence;

    public Task<OrderNumber> NextAsync(int year, CancellationToken cancellationToken)
        => Task.FromResult(OrderNumber.From(year, _next++));
}
