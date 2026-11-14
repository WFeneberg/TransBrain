using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.ExecuteImport;
using TransBrain.Application.Features.Imports.GetImportTargets;
using TransBrain.Application.Features.Imports.PreviewImport;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The import wizard behind the global system area: a system administrator imports into the
/// tenant named in the route, and the created rows land in exactly that tenant — visible to its
/// users through the regular endpoints.
/// </summary>
public class ImportEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static readonly Guid Tenant = TenantDefaults.DefaultTenantId;

    private static string Url(string path = "") => $"/api/system/tenants/{Tenant}/imports{path}";

    private const string Csv =
        "Firma;Strasse;PLZ;Ort;Zahlungsziel\n"
        + "Import Kunde Eins GmbH;Hauptstr. 1;80331;München;30\n"
        + "Import Kunde Zwei KG;Ring 2;10115;Berlin;14\n";

    private static readonly object[] Mappings =
    [
        new { targetField = "name", sourceColumn = "Firma" },
        new { targetField = "street", sourceColumn = "Strasse" },
        new { targetField = "postalCode", sourceColumn = "PLZ" },
        new { targetField = "city", sourceColumn = "Ort" },
        new { targetField = "country", sourceColumn = (string?)null, constantValue = "DE" },
        new { targetField = "paymentTermDays", sourceColumn = "Zahlungsziel" }
    ];

    private static MultipartFormDataContent FileUpload(string fileName, string content)
    {
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { fileContent, "file", fileName } };
    }

    [Fact]
    public async Task ImportFlow_UploadPreviewExecute_CreatesCustomersThroughTheRegularPipeline()
    {
        HttpClient system = factory.CreateSystemAdminClient();

        // 1. Upload + analysis
        HttpResponseMessage upload = await system.PostAsync(Url("/files"), FileUpload("kunden.csv", Csv));
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        upload.Headers.Location!.ToString().Should().StartWith($"/api/system/tenants/{Tenant}/imports/");
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;
        session.SourceType.Should().Be("Csv");
        session.Status.Should().Be("Ready");
        ImportTableResponse table = session.Tables!.Single();
        table.Name.Should().Be("kunden");
        table.Columns.Should().Contain("Firma");
        table.RowCount.Should().Be(2);
        table.SampleRows.Should().NotBeEmpty();

        // 2. Preview: everything valid, nothing imported yet
        HttpResponseMessage preview = await system.PostAsJsonAsync(
            Url($"/{session.Id}/preview"),
            new { tableName = "kunden", targetEntity = "Customers", mappings = Mappings });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportPreviewResponse previewBody = (await preview.Content.ReadFromJsonAsync<ImportPreviewResponse>())!;
        previewBody.TotalRows.Should().Be(2);
        previewBody.ValidRows.Should().Be(2);
        previewBody.InvalidRows.Should().Be(0);
        previewBody.Rows.Should().HaveCount(2);
        previewBody.Rows[0].Values["country"].Should().Be("DE");

        // 3. Execute
        HttpResponseMessage execute = await system.PostAsJsonAsync(
            Url($"/{session.Id}/execute"),
            new { tableName = "kunden", targetEntity = "Customers", mappings = Mappings });
        execute.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportExecutionResponse result = (await execute.Content.ReadFromJsonAsync<ImportExecutionResponse>())!;
        result.Status.Should().Be("Completed");
        result.ImportedCount.Should().Be(2);
        result.FailedCount.Should().Be(0);

        // 4. The customers exist in the target tenant, created through the very same path as
        // manual entry — a viewer of that tenant sees them through the regular endpoint.
        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/customers?pageSize=100");
        PagedResult<CustomerResponse> page = (await list.Content.ReadFromJsonAsync<PagedResult<CustomerResponse>>())!;
        page.Items.Should().Contain(c => c.Name == "Import Kunde Eins GmbH" && c.PaymentTermDays == 30);
        page.Items.Should().Contain(c => c.Name == "Import Kunde Zwei KG");

        // 5. The session history reflects the execution
        HttpResponseMessage get = await system.GetAsync(Url($"/{session.Id}"));
        ImportSessionResponse final = (await get.Content.ReadFromJsonAsync<ImportSessionResponse>())!;
        final.Status.Should().Be("Completed");
        final.TargetEntity.Should().Be("Customers");
        final.ImportedCount.Should().Be(2);
        final.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteImport_WithPartiallyInvalidRows_ImportsTheValidOnesAndReportsRowErrors()
    {
        HttpClient system = factory.CreateSystemAdminClient();
        const string csv =
            "Firma;Strasse;PLZ;Ort;Zahlungsziel\n"
            + "Teilimport Gut GmbH;Weg 1;50667;Köln;30\n"
            + ";Weg 2;50667;Köln;30\n";

        HttpResponseMessage upload = await system.PostAsync(Url("/files"), FileUpload("teil.csv", csv));
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;

        HttpResponseMessage execute = await system.PostAsJsonAsync(
            Url($"/{session.Id}/execute"),
            new { tableName = "teil", targetEntity = "Customers", mappings = Mappings });

        ImportExecutionResponse result = (await execute.Content.ReadFromJsonAsync<ImportExecutionResponse>())!;
        result.Status.Should().Be("CompletedWithErrors");
        result.ImportedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.RowNumber == 2);
    }

    [Fact]
    public async Task GetImportTargets_AsSystemAdmin_DescribesTheMappingVocabulary()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient().GetAsync(Url("/targets"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportTargetResponse[] targets = (await response.Content.ReadFromJsonAsync<ImportTargetResponse[]>())!;
        targets.Select(t => t.Entity).Should().BeEquivalentTo("Customers", "Drivers", "Vehicles");
        targets.Single(t => t.Entity == "Vehicles").Fields.Should().Contain(f => f.Name == "licensePlate" && f.Required);
    }

    [Fact]
    public async Task ListImportSessions_AfterUpload_ContainsTheSessionWithoutTables()
    {
        HttpClient system = factory.CreateSystemAdminClient();
        await system.PostAsync(Url("/files"), FileUpload("verlauf.csv", Csv));

        HttpResponseMessage list = await system.GetAsync(Url());

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportSessionResponse[] sessions = (await list.Content.ReadFromJsonAsync<ImportSessionResponse[]>())!;
        ImportSessionResponse entry = sessions.First(s => s.SourceName == "verlauf.csv");
        entry.Tables.Should().BeNull();
        entry.CreatedBy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ImportSessions_BelongToTheTenantInTheRoute_NotToAnotherTenant()
    {
        HttpClient system = factory.CreateSystemAdminClient();
        Guid otherTenant = Guid.CreateVersion7();
        HttpResponseMessage upload = await system.PostAsync(
            $"/api/system/tenants/{otherTenant}/imports/files", FileUpload("getrennt.csv", Csv));
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;

        // Visible under the tenant it was created for, invisible under the default tenant.
        (await system.GetAsync($"/api/system/tenants/{otherTenant}/imports/{session.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await system.GetAsync(Url($"/{session.Id}"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteImportSession_RemovesItFromTheHistory()
    {
        HttpClient system = factory.CreateSystemAdminClient();
        HttpResponseMessage upload = await system.PostAsync(Url("/files"), FileUpload("weg.csv", Csv));
        ImportSessionResponse session = (await upload.Content.ReadFromJsonAsync<ImportSessionResponse>())!;

        HttpResponseMessage delete = await system.DeleteAsync(Url($"/{session.Id}"));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage get = await system.GetAsync(Url($"/{session.Id}"));
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadImportFile_WithUnsupportedExtension_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient()
            .PostAsync(Url("/files"), FileUpload("bericht.pdf", "egal"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDatabaseImport_WithUnknownProvider_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient().PostAsJsonAsync(
            Url("/databases"), new { provider = "oracle", connectionString = "Host=x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDatabaseImport_WithUnreachableServer_ReturnsServiceUnavailableWithoutLeakingSecrets()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient().PostAsJsonAsync(
            Url("/databases"),
            new
            {
                provider = "PostgreSql",
                connectionString = "Host=127.0.0.1;Port=1;Database=nix;Username=u;Password=supergeheim;Timeout=1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("supergeheim");
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task ImportEndpoints_WithTenantRole_ReturnForbidden(string role)
    {
        HttpClient client = factory.CreateClientAs(role);

        (await client.GetAsync(Url())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync(Url("/targets"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync(Url("/files"), FileUpload("kunden.csv", Csv)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ImportEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        HttpClient anonymous = factory.CreateClient();

        (await anonymous.GetAsync(Url())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
