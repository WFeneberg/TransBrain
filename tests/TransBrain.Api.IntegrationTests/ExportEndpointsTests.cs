using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Exports;
using TransBrain.Application.Features.Imports;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The export download behind the global system area: a system administrator exports the data
/// of the tenant named in the route, in a shape the import accepts back as-is.
/// </summary>
public class ExportEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static readonly Guid Tenant = TenantDefaults.DefaultTenantId;

    private static string ExportUrl(string path) => $"/api/system/tenants/{Tenant}/exports{path}";

    private static string ImportUrl(string path) => $"/api/system/tenants/{Tenant}/imports{path}";

    private const string Csv =
        "Name;Straße;PLZ;Ort;Land;Zahlungsziel (Tage)\n"
        + "Export Kunde Eins GmbH;Hauptstr. 1;80331;München;DE;30\n"
        + "Export Kunde Zwei KG;Ring 2;10115;Berlin;DE;14\n";

    private static readonly object[] Mappings =
    [
        new { targetField = "name", sourceColumn = "Name" },
        new { targetField = "street", sourceColumn = "Straße" },
        new { targetField = "postalCode", sourceColumn = "PLZ" },
        new { targetField = "city", sourceColumn = "Ort" },
        new { targetField = "country", sourceColumn = "Land" },
        new { targetField = "paymentTermDays", sourceColumn = "Zahlungsziel (Tage)" }
    ];

    private async Task<HttpClient> SystemAdminWithImportedCustomersAsync()
    {
        HttpClient system = factory.CreateSystemAdminClient();

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(Csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        HttpResponseMessage upload = await system.PostAsync(
            ImportUrl("/files"), new MultipartFormDataContent { { fileContent, "file", "kunden.csv" } });
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;
        HttpResponseMessage execute = await system.PostAsJsonAsync(
            ImportUrl($"/{session.Id}/execute"),
            new { tableName = "kunden", targetEntity = "Customers", mappings = Mappings });
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        return system;
    }

    [Fact]
    public async Task GetExportTargets_AsSystemAdmin_ListsEntitiesWithRowCounts()
    {
        HttpClient system = await SystemAdminWithImportedCustomersAsync();

        HttpResponseMessage response = await system.GetAsync(ExportUrl("/targets"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ExportTargetResponse[] targets = (await response.Content.ReadFromJsonAsync<ExportTargetResponse[]>())!;
        targets.Select(t => t.Entity).Should().BeEquivalentTo("Customers", "Drivers", "Vehicles");
        targets.Single(t => t.Entity == "Customers").Label.Should().Be("Kunden");
        targets.Single(t => t.Entity == "Customers").RowCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExportCustomersAsCsv_ReturnsADownloadWithGermanHeadersAndTheRows()
    {
        HttpClient system = await SystemAdminWithImportedCustomersAsync();

        HttpResponseMessage response = await system.GetAsync(ExportUrl("/customers?format=csv"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should()
            .MatchRegex(@"^transbrain-export-kunden-\d{8}-\d{6}\.csv$");

        byte[] raw = await response.Content.ReadAsByteArrayAsync();
        raw.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        string csv = Encoding.UTF8.GetString(raw).TrimStart('\uFEFF');
        csv.Should().StartWith("Name;Straße;PLZ;Ort;Land;");
        csv.Should().Contain("Export Kunde Eins GmbH;Hauptstr. 1;80331;München;DE;;;;;30");
        csv.Should().Contain("Export Kunde Zwei KG;Ring 2;10115;Berlin;DE;;;;;14");
    }

    [Fact]
    public async Task ExportedXlsx_CanBeUploadedToTheImportAgain_WithEveryColumnReadable()
    {
        HttpClient system = await SystemAdminWithImportedCustomersAsync();

        byte[] xlsx = await system.GetByteArrayAsync(ExportUrl("/customers?format=xlsx"));

        var fileContent = new ByteArrayContent(xlsx);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        HttpResponseMessage upload = await system.PostAsync(
            ImportUrl("/files"), new MultipartFormDataContent { { fileContent, "file", "kunden-export.xlsx" } });

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;
        session.Status.Should().Be("Ready");
        ImportTableResponse table = session.Tables!.Single();
        table.Name.Should().Be("Kunden");
        table.Columns.Should().ContainInOrder(
            "Name", "Straße", "PLZ", "Ort", "Land", "Ansprechpartner",
            "E-Mail", "Telefon", "UID-Nummer", "Zahlungsziel (Tage)");
        table.RowCount.Should().BeGreaterThanOrEqualTo(2);
        table.SampleRows.Should().Contain(row => row.Contains("Export Kunde Eins GmbH"));
    }

    [Fact]
    public async Task ExportData_WithUnknownFormat_ReturnsValidationProblem()
    {
        HttpClient system = factory.CreateSystemAdminClient();

        HttpResponseMessage response = await system.GetAsync(ExportUrl("/customers?format=pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportData_WithUnknownEntity_ReturnsValidationProblem()
    {
        HttpClient system = factory.CreateSystemAdminClient();

        HttpResponseMessage response = await system.GetAsync(ExportUrl("/invoices"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task ExportEndpoints_WithTenantRole_ReturnForbidden(string role)
    {
        HttpClient client = factory.CreateClientAs(role);

        (await client.GetAsync(ExportUrl("/targets"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync(ExportUrl("/customers"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        HttpClient anonymous = factory.CreateClient();

        (await anonymous.GetAsync(ExportUrl("/targets"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
