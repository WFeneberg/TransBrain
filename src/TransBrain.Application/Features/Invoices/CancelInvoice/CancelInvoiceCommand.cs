using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.CancelInvoice;

public sealed record CancelInvoiceCommand(Guid Id) : ICommand<InvoiceResponse>;
