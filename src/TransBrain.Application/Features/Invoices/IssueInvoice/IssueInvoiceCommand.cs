using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.IssueInvoice;

public sealed record IssueInvoiceCommand(Guid Id) : ICommand<InvoiceResponse>;
