using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.CancelInvoice;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class CancelInvoiceCommandHandlerTests
{
    [Fact]
    public async Task Handle_IssuedInvoice_CancelsAndSaves()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);
        CancelInvoiceCommandHandler handler = new(fixture.Invoices);

        Result<InvoiceResponse> result = await handler.Handle(
            new CancelInvoiceCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        fixture.Invoices.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CancelledInvoice_FreesItsOrdersForBilling()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        CancelInvoiceCommandHandler handler = new(fixture.Invoices);

        await handler.Handle(new CancelInvoiceCommand(invoice.Id), CancellationToken.None);

        IReadOnlyList<Guid> billed = await fixture.Invoices.GetBilledOrderIdsAsync(
            invoice.BilledOrderIds(), CancellationToken.None);
        billed.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        CancelInvoiceCommandHandler handler = new(fixture.Invoices);

        Result<InvoiceResponse> result = await handler.Handle(
            new CancelInvoiceCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }

    [Fact]
    public async Task Handle_PaidInvoice_ReturnsConflictAndSavesNothing()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);
        invoice.MarkPaid(InvoiceFixture.Now.AddDays(1));
        CancelInvoiceCommandHandler handler = new(fixture.Invoices);

        Result<InvoiceResponse> result = await handler.Handle(
            new CancelInvoiceCommand(invoice.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
        fixture.Invoices.SaveChangesCallCount.Should().Be(0);
    }
}
