using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.ExportInvoicePdf;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ExportInvoicePdfQueryHandlerTests
{
    private static (ExportInvoicePdfQueryHandler Handler, InvoiceFixture Fixture, StubInvoicePdfRenderer Renderer) CreateHandler()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        StubInvoicePdfRenderer renderer = new();
        return (new ExportInvoicePdfQueryHandler(fixture.Invoices, renderer), fixture, renderer);
    }

    [Fact]
    public async Task Handle_IssuedInvoice_ReturnsPdfDocument()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, StubInvoicePdfRenderer renderer) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be($"{invoice.InvoiceNumber.Value}.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.Content.Should().NotBeEmpty();
        renderer.Rendered.Should().BeSameAs(invoice);
    }

    [Fact]
    public async Task Handle_PaidInvoice_ReturnsPdfDocument()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, _) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);
        invoice.MarkPaid(InvoiceFixture.Now.AddDays(3));

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DraftInvoice_ReturnsConflict()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, StubInvoicePdfRenderer renderer) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Invoice.NotADocument");
        renderer.Rendered.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CancelledInvoice_ReturnsConflict()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, _) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);
        invoice.Cancel();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotADocument");
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        (ExportInvoicePdfQueryHandler handler, _, _) = CreateHandler();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }
}
