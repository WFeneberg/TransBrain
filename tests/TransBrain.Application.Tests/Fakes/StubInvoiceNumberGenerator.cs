using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Tests.Fakes;

public sealed class StubInvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private int _next;

    public StubInvoiceNumberGenerator(int firstSequence = 1) => _next = firstSequence;

    public Task<InvoiceNumber> NextAsync(int year, CancellationToken cancellationToken)
        => Task.FromResult(InvoiceNumber.From(year, _next++));
}
