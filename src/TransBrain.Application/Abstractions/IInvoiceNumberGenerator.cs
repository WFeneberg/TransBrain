using TransBrain.Domain.Billing;

namespace TransBrain.Application.Abstractions;

public interface IInvoiceNumberGenerator
{
    Task<InvoiceNumber> NextAsync(int year, CancellationToken cancellationToken);
}
