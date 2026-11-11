using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.MarkInvoicePaid;

internal sealed class MarkInvoicePaidCommandHandler(
    IInvoiceRepository repository,
    TimeProvider timeProvider)
    : ICommandHandler<MarkInvoicePaidCommand, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        MarkInvoicePaidCommand command,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{command.Id}'.");
        }

        Result<Unit> paid = invoice.MarkPaid(timeProvider.GetUtcNow());
        if (!paid.IsSuccess)
        {
            return paid.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return InvoiceResponse.From(invoice);
    }
}
