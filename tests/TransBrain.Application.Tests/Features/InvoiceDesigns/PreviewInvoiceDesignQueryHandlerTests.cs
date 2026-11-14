using AwesomeAssertions;
using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Application.Features.InvoiceDesigns.PreviewInvoiceDesign;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.InvoiceDesigns;

public class PreviewInvoiceDesignQueryHandlerTests
{
    private static (PreviewInvoiceDesignQueryHandler Handler, StubInvoicePdfRenderer Renderer) CreateHandler()
    {
        StubInvoicePdfRenderer renderer = new();
        FixedTimeProvider clock = new(InvoiceDesignFixture.Now);
        return (new PreviewInvoiceDesignQueryHandler(renderer, clock), renderer);
    }

    [Fact]
    public async Task Handle_ValidPayload_RendersSampleInvoiceWithDesign()
    {
        (PreviewInvoiceDesignQueryHandler handler, StubInvoicePdfRenderer renderer) = CreateHandler();

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new PreviewInvoiceDesignQuery(InvoiceDesignFixture.APayload()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("rechnungsdesign-vorschau.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.Content.Should().NotBeEmpty();
        renderer.RenderedWith.Should().NotBeNull();
        renderer.RenderedWith!.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        renderer.Rendered.Should().NotBeNull();
        renderer.Rendered!.Lines.Should().HaveCount(3);
        renderer.Rendered.Status.Should().Be(TransBrain.Domain.Billing.InvoiceStatus.Issued);
    }

    [Fact]
    public async Task Handle_InvalidPayload_ReturnsValidationErrorWithoutRendering()
    {
        (PreviewInvoiceDesignQueryHandler handler, StubInvoicePdfRenderer renderer) = CreateHandler();
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with { Iban = "" };

        Result<InvoiceDocumentResponse> result = await handler.Handle(
            new PreviewInvoiceDesignQuery(payload), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("InvoiceDesign.IbanRequired");
        renderer.Rendered.Should().BeNull();
    }
}
