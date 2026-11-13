using AwesomeAssertions;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.PreviewImport;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Features.Imports;

public class PreviewImportQueryHandlerTests
{
    private static readonly IReadOnlyList<string?> ValidRow =
        ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"];

    private static readonly IReadOnlyList<string?> InvalidRow =
        ["Maier KG", "Ring 2", "10115", "Berlin", "DE", "übermorgen"];

    [Fact]
    public async Task Send_PreviewImport_CountsAllRowsAndReportsPerRowErrors()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(ValidRow, InvalidRow, ValidRow));

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(3);
        result.Value.ValidRows.Should().Be(2);
        result.Value.InvalidRows.Should().Be(1);
        result.Value.Rows.Should().HaveCount(3);

        ImportPreviewRowResponse failing = result.Value.Rows.Single(r => r.Errors.Count > 0);
        failing.RowNumber.Should().Be(2);
        failing.Errors.Should().ContainSingle(e => e.StartsWith("paymentTermDays:"));
        failing.Values["name"].Should().Be("Maier KG");
    }

    [Fact]
    public async Task Send_PreviewImport_LimitsRowDetailsButCountsEverything()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        IReadOnlyList<string?>[] rows = [.. Enumerable.Repeat(ValidRow, 30)];
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(rows));

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings, MaxRows: 5),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(30);
        result.Value.ValidRows.Should().Be(30);
        result.Value.Rows.Should().HaveCount(5);
    }

    [Fact]
    public async Task Send_PreviewImportWithUnknownTable_ListsAvailableTables()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(ValidRow));

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Lieferanten", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.UnknownTable");
        result.Error.Message.Should().Contain("Kunden");
    }

    [Fact]
    public async Task Send_PreviewImportWithUnknownTarget_ReturnsUnknownTarget()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(ValidRow));

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Kunden", "Suppliers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.UnknownTarget");
    }

    [Fact]
    public async Task Send_PreviewImportWithoutMappings_ReturnsValidationFailure()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(ValidRow));

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Kunden", "Customers", []), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Validation.Failed");
    }

    [Fact]
    public async Task Send_PreviewImportForUnknownSession_ReturnsNotFound()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<ImportPreviewResponse> result = await context.Sender.Send(
            new PreviewImportQuery(Guid.NewGuid(), "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.SessionNotFound");
    }

    [Fact]
    public async Task Send_PreviewImport_DoesNotCreateAnyEntities()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(ValidRow));

        await context.Sender.Send(
            new PreviewImportQuery(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        context.Customers.Customers.Should().BeEmpty();
        session.Status.Should().Be(ImportSessionStatus.Ready);
    }
}
