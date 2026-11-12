using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid Id) : IQuery<InvoiceResponse>;
