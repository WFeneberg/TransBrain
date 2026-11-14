using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Features.Exports;
using TransBrain.Application.Features.Imports;

namespace TransBrain.Api.IntegrationTests;

public class ExportEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
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

    private async Task<HttpClient> AdminWithImportedCustomersAsync()
    {
        HttpClient admin = factory.CreateClientAs("admin");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(Csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        HttpResponseMessage upload = await admin.PostAsync(
            "/api/imports/files", new MultipartFormDataContent { { fileContent, "file", "kunden.csv" } });
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;
        HttpResponseMessage execute = await admin.PostAsJsonAsync(
            $"/api/imports/{session.Id}/execute",
            new { tableName = "kunden", targetEntity = "Customers", mappings = Mappings });
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        return admin;
    }

    [Fact]
    public async Task GetExportTargets_AsAdmin_ListsEntitiesWithRowCounts()
    {
        HttpClient admin = await AdminWithImportedCustomersAsync();

        HttpResponseMessage response = await admin.GetAsync("/api/exports/targets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ExportTargetResponse[] targets = (await response.Content.ReadFromJsonAsync<ExportTargetResponse[]>())!;
        targets.Select(t => t.Entity).Should().BeEquivalentTo("Customers", "Drivers", "Vehicles");
        targets.Single(t => t.Entity == "Customers").Label.Should().Be("Kunden");
        targets.Single(t => t.Entity == "Customers").RowCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExportCustomersAsCsv_ReturnsADownloadWithGermanHeadersAndTheRows()
    {
        HttpClient admin = await AdminWithImportedCustomersAsync();

        HttpResponseMessage response = await admin.GetAsync("/api/exports/customers?format=csv");

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
        HttpClient admin = await AdminWithImportedCustomersAsync();

        byte[] xlsx = await admin.GetByteArrayAsync("/api/exports/customers?format=xlsx");

        var fileContent = new ByteArrayContent(xlsx);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        HttpResponseMessage upload = await admin.PostAsync(
            "/api/imports/files", new MultipartFormDataContent { { fileContent, "file", "kunden-export.xlsx" } });

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
        HttpClient admin = factory.CreateClientAs("admin");

        HttpResponseMessage response = await admin.GetAsync("/api/exports/customers?format=pdf");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportData_WithUnknownEntity_ReturnsValidationProblem()
    {
        HttpClient admin = factory.CreateClientAs("admin");

        HttpResponseMessage response = await admin.GetAsync("/api/exports/invoices");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("viewer")]
    [InlineData("disponent")]
    public async Task ExportEndpoints_WithoutMasterDataWriteRole_ReturnForbidden(string role)
    {
        HttpClient client = factory.CreateClientAs(role);

        (await client.GetAsync("/api/exports/targets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/exports/customers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        HttpClient anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/exports/targets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
