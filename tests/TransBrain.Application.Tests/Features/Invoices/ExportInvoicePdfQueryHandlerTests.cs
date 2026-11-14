using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.ExportInvoicePdf;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Application.Tests.Features.InvoiceDesigns;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ExportInvoicePdfQueryHandlerTests
{
    private static (ExportInvoicePdfQueryHandler Handler, InvoiceFixture Fixture, StubInvoicePdfRenderer Renderer, InMemoryInvoiceDesignRepository Designs) CreateHandler()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        StubInvoicePdfRenderer renderer = new();
        InMemoryInvoiceDesignRepository designs = new();
        return (new ExportInvoicePdfQueryHandler(fixture.Invoices, designs, renderer), fixture, renderer, designs);
    }

    [Fact]
    public async Task Handle_IssuedInvoice_ReturnsPdfDocument()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, StubInvoicePdfRenderer renderer, InMemoryInvoiceDesignRepository designs) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be($"{invoice.InvoiceNumber.Value}.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.Content.Should().NotBeEmpty();
        renderer.Rendered.Should().BeSameAs(invoice);
        renderer.RenderedWith.Should().BeSameAs(designs.Default);
    }

    [Fact]
    public async Task Handle_SavedDesign_RendersWithIt()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, StubInvoicePdfRenderer renderer, InMemoryInvoiceDesignRepository designs) = CreateHandler();
        InvoiceDesign saved = InvoiceDesignFixture.ADesign("Eigene Firma GmbH");
        designs.Seed(saved);
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        renderer.RenderedWith.Should().BeSameAs(saved);
    }

    [Fact]
    public async Task Handle_PaidInvoice_ReturnsPdfDocument()
    {
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, _, _) = CreateHandler();
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
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, StubInvoicePdfRenderer renderer, _) = CreateHandler();
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
        (ExportInvoicePdfQueryHandler handler, InvoiceFixture fixture, _, _) = CreateHandler();
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
        (ExportInvoicePdfQueryHandler handler, _, _, _) = CreateHandler();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoicePdfQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }
}
