using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Invoices;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.InvoiceDesigns.PreviewInvoiceDesign;

internal sealed class PreviewInvoiceDesignQueryHandler(
    IInvoicePdfRenderer renderer,
    TimeProvider clock)
    : IQueryHandler<PreviewInvoiceDesignQuery, InvoiceDocumentResponse>
{
    public Task<Result<InvoiceDocumentResponse>> Handle(
        PreviewInvoiceDesignQuery query,
        CancellationToken cancellationToken)
    {
        Result<InvoiceDesign> design = query.Design.ToNewDesign(clock.GetUtcNow());
        if (!design.IsSuccess)
        {
            return Task.FromResult<Result<InvoiceDocumentResponse>>(design.Error!);
        }

        Invoice sample = SampleInvoice(clock.GetUtcNow());

        return Task.FromResult<Result<InvoiceDocumentResponse>>(new InvoiceDocumentResponse(
            "rechnungsdesign-vorschau.pdf",
            "application/pdf",
            renderer.Render(sample, design.Value)));
    }

    /// <summary>
    /// A representative issued invoice over obviously fictitious data ("Muster..."), built through
    /// the same domain factories as real invoices so the preview can never drift from what the
    /// renderer produces in production.
    /// </summary>
    private static Invoice SampleInvoice(DateTimeOffset now)
    {
        Customer customer = Customer.Create(
            "Musterkunde Handels GmbH",
            Address.Create("Musterkunde Handels GmbH", "Beispielallee 42", "20095", "Hamburg", "DE").Value,
            null,
            null,
            null,
            "DE999999999",
            14).Value;

        Invoice invoice = Invoice.Create(InvoiceNumber.From(now.Year, 123), customer, 19m, now).Value;

        invoice.AddLine(SampleOrder(now, 101), 1250.00m, "Transport München – Hamburg, Planenauflieger");
        invoice.AddLine(SampleOrder(now, 102), 890.50m, "Transport Augsburg – Berlin, Teilladung");
        invoice.AddLine(SampleOrder(now, 103), 310.00m, "Zuschlag Ladungssicherung und Wartezeit");

        invoice.Issue(now);
        return invoice;
    }

    private static TransportOrder SampleOrder(DateTimeOffset now, int sequence)
    {
        DateTimeOffset pickup = now.AddDays(-7);
        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(now.Year, sequence),
            Address.Create("Musterwerk GmbH", "Industriestraße 1", "80339", "München", "DE").Value,
            Address.Create("Musterkunde Handels GmbH", "Beispielallee 42", "20095", "Hamburg", "DE").Value,
            Cargo.Create("Palettenware", 12_000, 13.6m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(6), pickup.AddHours(10)).Value,
            now.AddDays(-14)).Value;

        order.MarkPlanned();
        order.MarkInTransit();
        order.MarkDelivered();
        return order;
    }
}
