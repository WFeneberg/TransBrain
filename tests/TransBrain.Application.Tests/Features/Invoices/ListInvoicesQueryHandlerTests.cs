using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.ListInvoices;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ListInvoicesQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoFilter_ReturnsNewestInvoiceNumberFirst()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        fixture.DraftInvoice(invoiceSequence: 2, orderSequence: 2);
        ListInvoicesQueryHandler handler = new(fixture.Invoices);

        Result<PagedResult<InvoiceResponse>> result = await handler.Handle(
            new ListInvoicesQuery(1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(i => i.InvoiceNumber)
            .Should().Equal("RE-2027-00002", "RE-2027-00001");
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingInvoicesCaseInsensitively()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        Invoice issued = fixture.DraftInvoice(invoiceSequence: 2, orderSequence: 2);
        issued.Issue(InvoiceFixture.Now);
        ListInvoicesQueryHandler handler = new(fixture.Invoices);

        Result<PagedResult<InvoiceResponse>> result = await handler.Handle(
            new ListInvoicesQuery(1, 20, "issued"), CancellationToken.None);

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().Status.Should().Be("Issued");
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("99")]
    public async Task Handle_UnknownStatus_ReturnsValidationError(string status)
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        ListInvoicesQueryHandler handler = new(fixture.Invoices);

        Result<PagedResult<InvoiceResponse>> result = await handler.Handle(
            new ListInvoicesQuery(1, 20, status), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Invoice.UnknownStatus");
    }

    [Fact]
    public async Task Handle_CustomerFilter_ReturnsOnlyThatCustomersInvoices()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        ListInvoicesQueryHandler handler = new(fixture.Invoices);

        Result<PagedResult<InvoiceResponse>> match = await handler.Handle(
            new ListInvoicesQuery(1, 20, null, fixture.Customer.Id), CancellationToken.None);
        Result<PagedResult<InvoiceResponse>> noMatch = await handler.Handle(
            new ListInvoicesQuery(1, 20, null, Guid.NewGuid()), CancellationToken.None);

        match.Value.TotalCount.Should().Be(1);
        noMatch.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SecondPage_SkipsTheFirst()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        fixture.DraftInvoice(invoiceSequence: 2, orderSequence: 2);
        ListInvoicesQueryHandler handler = new(fixture.Invoices);

        Result<PagedResult<InvoiceResponse>> result = await handler.Handle(
            new ListInvoicesQuery(2, 1), CancellationToken.None);

        result.Value.Items.Single().InvoiceNumber.Should().Be("RE-2027-00001");
        result.Value.TotalCount.Should().Be(2);
    }
}
