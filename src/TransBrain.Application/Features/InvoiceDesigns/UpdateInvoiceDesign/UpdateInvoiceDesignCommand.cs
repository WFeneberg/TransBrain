using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;

public sealed record UpdateInvoiceDesignCommand(InvoiceDesignPayload Design) : ICommand<InvoiceDesignResponse>;
