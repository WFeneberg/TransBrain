namespace TransBrain.Application.Features.Invoices;

/// <summary>A downloadable rendition of an invoice (PDF or XRechnung XML).</summary>
public sealed record InvoiceDocumentResponse(string FileName, string ContentType, byte[] Content);
