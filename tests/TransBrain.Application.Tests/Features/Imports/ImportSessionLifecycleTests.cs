using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.CreateImportSession;
using TransBrain.Application.Features.Imports.DeleteImportSession;
using TransBrain.Application.Features.Imports.GetImportSession;
using TransBrain.Application.Features.Imports.GetImportTargets;
using TransBrain.Application.Features.Imports.ListImportSessions;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Features.Imports;

public class ImportSessionLifecycleTests
{
    private static readonly byte[] SomeBytes = "Firma;Ort\nHuber;München"u8.ToArray();

    [Fact]
    public async Task Send_CreateFileImportSession_ParsesAndPersistsSession()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        context.Parser.NextFileResult = ImportTestSetup.CustomerTables(
            ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"]);

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand("Kunden.CSV", SomeBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be("Csv");
        result.Value.SourceName.Should().Be("Kunden.CSV");
        result.Value.Status.Should().Be("Ready");
        result.Value.CreatedBy.Should().Be("importer-admin");
        result.Value.Tables.Should().ContainSingle(t => t.Name == "Kunden" && t.RowCount == 1);
        context.Sessions.Sessions.Should().ContainSingle();
        context.Parser.LastSourceType.Should().Be(ImportSourceType.Csv);
    }

    [Theory]
    [InlineData("daten.xlsx", "Excel")]
    [InlineData("firma.accdb", "Access")]
    [InlineData("alt.mdb", "Access")]
    [InlineData("daten.sqlite", "Sqlite")]
    [InlineData("export.json", "Json")]
    [InlineData("export.xml", "Xml")]
    public async Task Send_CreateFileImportSession_ResolvesSourceTypeFromExtension(
        string fileName, string expectedSourceType)
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        context.Parser.NextFileResult = ImportTestSetup.CustomerTables(
            ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"]);

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand(fileName, SomeBytes), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be(expectedSourceType);
    }

    [Fact]
    public async Task Send_CreateFileImportSessionWithUnknownExtension_ReturnsValidationFailure()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand("daten.pdf", SomeBytes), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Validation.Failed");
        result.Error.Failures.Should().ContainKey(nameof(CreateFileImportSessionCommand.FileName));
    }

    [Fact]
    public async Task Send_CreateFileImportSessionWithEmptyContent_ReturnsValidationFailure()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand("daten.csv", []), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Validation.Failed");
    }

    [Fact]
    public async Task Send_CreateFileImportSessionWhenParserFindsNoTables_ReturnsNoTablesError()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        context.Parser.NextFileResult = new ImportTableSet([]);

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand("leer.csv", SomeBytes), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.NoTables");
        context.Sessions.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_CreateFileImportSessionWhenParserFails_PropagatesParserError()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        context.Parser.NextFileResult = Error.Validation("Import.InvalidJson", "broken");

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateFileImportSessionCommand("daten.json", SomeBytes), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidJson");
    }

    [Fact]
    public async Task Send_CreateDatabaseImportSession_ReadsDatabaseAndRedactsConnectionString()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        context.Parser.NextDatabaseResult = ImportTestSetup.CustomerTables(
            ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"]);

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateDatabaseImportSessionCommand(
                "postgresql", "Host=legacy-db;Database=altsystem;Username=sa;Password=geheim"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be("Database");
        result.Value.SourceName.Should().Be("PostgreSql: legacy-db/altsystem");
        result.Value.SourceName.Should().NotContainAny("geheim", "Password", "sa");
        context.Parser.LastProvider.Should().Be(DatabaseImportProvider.PostgreSql);
    }

    [Fact]
    public async Task Send_CreateDatabaseImportSessionWithUnknownProvider_ReturnsValidationFailure()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new CreateDatabaseImportSessionCommand("oracle", "Host=x"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Validation.Failed");
        result.Error.Failures.Should().ContainKey(nameof(CreateDatabaseImportSessionCommand.Provider));
    }

    [Theory]
    [InlineData(DatabaseImportProvider.PostgreSql, "Host=legacy;Port=5432;Database=alt;Password=x", "PostgreSql: legacy/alt")]
    [InlineData(DatabaseImportProvider.SqlServer, "Server=sql01;Initial Catalog=Spedition;User Id=sa;Password=x", "SqlServer: sql01/Spedition")]
    [InlineData(DatabaseImportProvider.Odbc, "DSN=AltesSystem;UID=user;PWD=x", "Odbc: AltesSystem")]
    [InlineData(DatabaseImportProvider.MySql, "nicht=parsebar;;;=", "MySql")]
    public void BuildDisplayName_KeepsOnlyHostAndDatabase(
        DatabaseImportProvider provider, string connectionString, string expected)
    {
        string displayName = CreateDatabaseImportSessionCommandHandler.BuildDisplayName(provider, connectionString);

        displayName.Should().Be(expected);
        displayName.Should().NotContainAny("Password", "PWD", "User Id");
    }

    [Fact]
    public async Task Send_GetImportSession_ReturnsSessionWithTablesAndSampleRows()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        IReadOnlyList<string?>[] rows = [.. Enumerable.Range(1, 8).Select(i =>
            (IReadOnlyList<string?>)[$"Firma {i}", "Str. 1", "80331", "München", "DE", "30"])];
        context.Sessions.Seed(session, ImportTestSetup.CustomerTables(rows));

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new GetImportSessionQuery(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ImportTableResponse table = result.Value.Tables!.Single();
        table.RowCount.Should().Be(8);
        table.SampleRows.Should().HaveCount(ImportLimits.SampleRows);
    }

    [Fact]
    public async Task Send_GetUnknownImportSession_ReturnsNotFound()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<ImportSessionResponse> result = await context.Sender.Send(
            new GetImportSessionQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.SessionNotFound");
    }

    [Fact]
    public async Task Send_ListImportSessions_ReturnsNewestFirstWithoutTables()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession older = ImportSession.Create(
            ImportSourceType.Csv, "alt.csv", "importer-admin",
            FixedTimeProvider.Default.AddHours(-1)).Value;
        ImportSession newer = ImportSession.Create(
            ImportSourceType.Json, "neu.json", "importer-admin", FixedTimeProvider.Default).Value;
        context.Sessions.Seed(older, new ImportTableSet([]));
        context.Sessions.Seed(newer, new ImportTableSet([]));

        Result<IReadOnlyList<ImportSessionResponse>> result = await context.Sender.Send(
            new ListImportSessionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].SourceName.Should().Be("neu.json");
        result.Value[0].Tables.Should().BeNull();
    }

    [Fact]
    public async Task Send_DeleteImportSession_RemovesSession()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();
        ImportSession session = ImportTestSetup.CreateSession();
        context.Sessions.Seed(session, new ImportTableSet([]));

        Result<Unit> result = await context.Sender.Send(
            new DeleteImportSessionCommand(session.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sessions.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_DeleteUnknownImportSession_ReturnsNotFound()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<Unit> result = await context.Sender.Send(
            new DeleteImportSessionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.SessionNotFound");
    }

    [Fact]
    public async Task Send_GetImportTargets_DescribesAllTargetsForTheMappingUi()
    {
        ImportTestSetup.TestContext context = ImportTestSetup.Build();

        Result<IReadOnlyList<ImportTargetResponse>> result = await context.Sender.Send(
            new GetImportTargetsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Entity).Should().BeEquivalentTo("Customers", "Drivers", "Vehicles");

        ImportTargetResponse drivers = result.Value.Single(t => t.Entity == "Drivers");
        drivers.Label.Should().Be("Fahrer");
        ImportTargetFieldResponse licenseClasses = drivers.Fields.Single(f => f.Name == "licenseClasses");
        licenseClasses.Kind.Should().Be("TextList");
        licenseClasses.Required.Should().BeTrue();
        licenseClasses.AllowedValues.Should().Contain("CE");
    }
}
