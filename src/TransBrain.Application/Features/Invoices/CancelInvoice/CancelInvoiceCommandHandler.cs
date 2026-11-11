using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.CancelInvoice;

internal sealed class CancelInvoiceCommandHandler(IInvoiceRepository repository)
    : ICommandHandler<CancelInvoiceCommand, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        CancelInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{command.Id}'.");
        }

        Result<Unit> cancelled = invoice.Cancel();
        if (!cancelled.IsSuccess)
        {
            return cancelled.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return InvoiceResponse.From(invoice);
    }
}
