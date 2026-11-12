using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.MarkInvoicePaid;

public sealed record MarkInvoicePaidCommand(Guid Id) : ICommand<InvoiceResponse>;
