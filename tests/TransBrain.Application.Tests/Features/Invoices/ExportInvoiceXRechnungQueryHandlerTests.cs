using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.ExportInvoiceXRechnung;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Application.Tests.Features.InvoiceDesigns;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ExportInvoiceXRechnungQueryHandlerTests
{
    private static (ExportInvoiceXRechnungQueryHandler Handler, InvoiceFixture Fixture, StubXRechnungWriter Writer, InMemoryInvoiceDesignRepository Designs) CreateHandler()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        StubXRechnungWriter writer = new();
        InMemoryInvoiceDesignRepository designs = new();
        return (new ExportInvoiceXRechnungQueryHandler(fixture.Invoices, designs, writer), fixture, writer, designs);
    }

    [Fact]
    public async Task Handle_IssuedInvoice_ReturnsXmlDocument()
    {
        (ExportInvoiceXRechnungQueryHandler handler, InvoiceFixture fixture, StubXRechnungWriter writer, InMemoryInvoiceDesignRepository designs) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoiceXRechnungQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be($"{invoice.InvoiceNumber.Value}-xrechnung.xml");
        result.Value.ContentType.Should().Be("application/xml");
        result.Value.Content.Should().NotBeEmpty();
        writer.Written.Should().BeSameAs(invoice);
        writer.WrittenWith.Should().BeSameAs(designs.Default);
    }

    [Fact]
    public async Task Handle_SavedDesign_WritesWithIt()
    {
        (ExportInvoiceXRechnungQueryHandler handler, InvoiceFixture fixture, StubXRechnungWriter writer, InMemoryInvoiceDesignRepository designs) = CreateHandler();
        InvoiceDesign saved = InvoiceDesignFixture.ADesign("Eigene Firma GmbH");
        designs.Seed(saved);
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoiceXRechnungQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        writer.WrittenWith.Should().BeSameAs(saved);
    }

    [Fact]
    public async Task Handle_DraftInvoice_ReturnsConflict()
    {
        (ExportInvoiceXRechnungQueryHandler handler, InvoiceFixture fixture, StubXRechnungWriter writer, _) = CreateHandler();
        Invoice invoice = fixture.DraftInvoice();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoiceXRechnungQuery(invoice.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotADocument");
        writer.Written.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        (ExportInvoiceXRechnungQueryHandler handler, _, _, _) = CreateHandler();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new ExportInvoiceXRechnungQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }
}
