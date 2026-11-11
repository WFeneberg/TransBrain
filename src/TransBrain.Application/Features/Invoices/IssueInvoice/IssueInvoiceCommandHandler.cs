using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.IssueInvoice;

internal sealed class IssueInvoiceCommandHandler(
    IInvoiceRepository repository,
    TimeProvider timeProvider)
    : ICommandHandler<IssueInvoiceCommand, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        IssueInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{command.Id}'.");
        }

        Result<Unit> issued = invoice.Issue(timeProvider.GetUtcNow());
        if (!issued.IsSuccess)
        {
            return issued.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return InvoiceResponse.From(invoice);
    }
}
