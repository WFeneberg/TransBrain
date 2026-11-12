using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.CreateInvoice;

public sealed record InvoiceLinePayload(Guid OrderId, decimal NetAmount, string? Description = null);

public sealed record CreateInvoiceCommand(
    Guid CustomerId,
    decimal VatRatePercent,
    IReadOnlyList<InvoiceLinePayload> Lines) : ICommand<InvoiceResponse>;
