using AwesomeAssertions;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.ExecuteImport;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Features.Imports;

public class ExecuteImportCommandHandlerTests
{
    private static readonly IReadOnlyList<string?> Huber =
        ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"];

    private static readonly IReadOnlyList<string?> Maier =
        ["Maier KG", "Ring 2", "10115", "Berlin", "DE", ""];

    [Fact]
    public async Task Send_ExecuteImport_CreatesEntitiesThroughTheRegularCommands()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(Huber, Maier));

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
        result.Value.TotalRows.Should().Be(2);
        result.Value.ImportedCount.Should().Be(2);
        result.Value.FailedCount.Should().Be(0);

        context.Customers.Customers.Should().HaveCount(2);
        Customer huber = context.Customers.Customers.Single(c => c.Name == "Huber GmbH");
        huber.Address.City.Should().Be("München");
        huber.PaymentTermDays.Should().Be(30);
        context.Customers.Customers.Single(c => c.Name == "Maier KG").PaymentTermDays.Should().Be(0);
    }

    [Fact]
    public async Task Send_ExecuteImport_RecordsExecutionOnTheSession()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(Huber));

        await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        session.Status.Should().Be(ImportSessionStatus.Completed);
        session.TargetEntity.Should().Be("Customers");
        session.TableName.Should().Be("Kunden");
        session.ImportedCount.Should().Be(1);
        session.CompletedAtUtc.Should().NotBeNull();
        context.Sessions.SaveChangesCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Send_ExecuteImportWithMixedRows_ImportsValidRowsAndReportsFailures()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        IReadOnlyList<string?> broken = ["", "Ring 2", "10115", "Berlin", "DE", "30"];
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(Huber, broken));

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("CompletedWithErrors");
        result.Value.ImportedCount.Should().Be(1);
        result.Value.FailedCount.Should().Be(1);
        result.Value.Errors.Should().ContainSingle(e => e.RowNumber == 2 && e.Message.StartsWith("name:"));
        context.Customers.Customers.Should().ContainSingle(c => c.Name == "Huber GmbH");
        session.Status.Should().Be(ImportSessionStatus.CompletedWithErrors);
    }

    [Fact]
    public async Task Send_ExecuteImportWhereEveryRowFails_MarksSessionFailed()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        IReadOnlyList<string?> broken = ["", "Ring 2", "10115", "Berlin", "DE", "30"];
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(broken));

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.Value.Status.Should().Be("Failed");
        session.Status.Should().Be(ImportSessionStatus.Failed);
        context.Customers.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_ExecuteImportRowRejectedByInnerCommandValidation_ReportsFieldErrors()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        // "XX" ist kein gültiges ISO-Land; der CreateCustomerCommand-Validator lehnt das ab.
        IReadOnlyList<string?> badCountry = ["Huber GmbH", "Hauptstr. 1", "80331", "München", "XX!", "30"];
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(badCountry));

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", ImportTestSetup.CustomerMappings),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ImportedCount.Should().Be(0);
        result.Value.FailedCount.Should().Be(1);
        result.Value.Errors.Should().ContainSingle(e => e.RowNumber == 1 && e.Message.Length > 0);
        context.Customers.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_ExecuteImportIntoDrivers_CreatesDriversWithParsedListsAndDates()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        var tables = new ImportTableSet([
            new ImportTable(
                "Fahrer",
                ["Vorname", "Name", "Klassen", "Gueltig"],
                [["Max", "Muster", "C1;CE", "13.11.2027"]])
        ]);
        context.Sessions.Seed(session, tables);

        IReadOnlyList<ImportFieldMapping> mappings =
        [
            new("firstName", "Vorname", null),
            new("lastName", "Name", null),
            new("licenseClasses", "Klassen", null),
            new("licenseValidUntil", "Gueltig", null)
        ];

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Fahrer", "Drivers", mappings), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ImportedCount.Should().Be(1);
        context.Drivers.Drivers.Should().ContainSingle(d => d.LastName == "Muster");
    }

    [Fact]
    public async Task Send_ExecuteImportIntoVehicles_CreatesVehiclesWithConstantDefaults()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        var tables = new ImportTableSet([
            new ImportTable(
                "Flotte",
                ["Kennzeichen", "Nutzlast", "Lademeter", "HU"],
                [["M-TB 100", "24000", "13,6", "2027-06-30"]])
        ]);
        context.Sessions.Seed(session, tables);

        IReadOnlyList<ImportFieldMapping> mappings =
        [
            new("licensePlate", "Kennzeichen", null),
            new("type", null, "Tractor"),
            new("payloadKg", "Nutzlast", null),
            new("loadMeters", "Lademeter", null),
            new("nextInspectionDue", "HU", null)
        ];

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Flotte", "Vehicles", mappings), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ImportedCount.Should().Be(1);
        context.Vehicles.Vehicles.Should().ContainSingle(v => v.LicensePlate.Value == "M-TB 100");
    }

    [Fact]
    public async Task Send_ExecuteImportWithUnmappedRequiredField_FailsBeforeImportingAnything()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(Huber));

        IReadOnlyList<ImportFieldMapping> incomplete =
            [.. ImportTestSetup.CustomerMappings.Where(m => m.TargetField != "city")];

        Result<ImportExecutionResponse> result = await context.Sender.Send(
            new ExecuteImportCommand(session.Id, "Kunden", "Customers", incomplete), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.RequiredFieldNotMapped");
        context.Customers.Customers.Should().BeEmpty();
        session.Status.Should().Be(ImportSessionStatus.Ready);
    }
}
