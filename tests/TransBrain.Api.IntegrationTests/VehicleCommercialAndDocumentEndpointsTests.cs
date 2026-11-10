using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.VehicleDocuments;

namespace TransBrain.Api.IntegrationTests;

public class VehicleCommercialAndDocumentEndpointsTests(TransBrainApiFactory factory)
    : IClassFixture<TransBrainApiFactory>
{
    private static object VehicleBody(string plate, object? commercial = null, string? dataSheet = null) => new
    {
        licensePlate = plate,
        type = "Tractor",
        payloadKg = 24_000,
        loadMeters = 13.6m,
        nextInspectionDue = "2027-03-31",
        commercial,
        dataSheet,
    };

    private async Task<VehicleResponse> CreateVehicleAsync(HttpClient admin, string plate)
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/vehicles", VehicleBody(plate));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    private static async Task<VehicleCatalogEntryResponse> CreateInsurerAsync(HttpClient admin, string code)
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync(
            "/api/vehicle-catalog/InsuranceCompany", new { code, name = $"{code} Versicherung", isActive = true });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse>())!;
    }

    [Fact]
    public async Task PutAndGetVehicle_CommercialDataAndDataSheet_RoundTrip()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        await CreateInsurerAsync(admin, "RT-INS");
        VehicleResponse created = await CreateVehicleAsync(admin, "M-KF 1");
        const string sheet = "Motor:      D13K\n  Leistung: 368 kW\n";

        HttpResponseMessage put = await admin.PutAsJsonAsync($"/api/vehicles/{created.Id}", VehicleBody(
            "M-KF 1",
            new
            {
                purchasePrice = 125_000.50m,
                purchasedOn = "2023-03-15",
                purchaseOdometerKm = 12_000,
                firstRegisteredOn = "2022-11-30",
                usefulLifeYears = 8,
                usefulLifeKm = 900_000,
                costCenter = "KST-4711",
                vehicleInsurer = "rt-ins",
                vehicleInsurancePolicyNumbers = "POL-1\nPOL-2",
                transportInsurer = "RT-INS",
                transportInsurancePolicyNumbers = "TR-9",
            },
            sheet));

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleResponse fetched = (await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<VehicleResponse>($"/api/vehicles/{created.Id}"))!;
        fetched.Commercial.Should().Be(new VehicleCommercialData(
            125_000.50m, new DateOnly(2023, 3, 15), 12_000, new DateOnly(2022, 11, 30), 8, 900_000,
            "KST-4711", "RT-INS", "POL-1\nPOL-2", "RT-INS", "TR-9"));
        fetched.DataSheet.Should().Be(sheet);

        await admin.PutAsJsonAsync($"/api/vehicles/{created.Id}", VehicleBody("M-KF 1"));
        VehicleResponse cleared = (await admin.GetFromJsonAsync<VehicleResponse>($"/api/vehicles/{created.Id}"))!;
        cleared.Commercial.Should().Be(new VehicleCommercialData());
        cleared.DataSheet.Should().BeNull();
    }

    [Fact]
    public async Task PostVehicle_InvalidCommercialData_ReturnsErrorsKeyedByDottedFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync(
            "/api/vehicles",
            VehicleBody("M-KF 2", new { purchasePrice = 100.001m, usefulLifeYears = 0 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"Commercial.PurchasePrice\"").And.Contain("\"Commercial.UsefulLifeYears\"");
    }

    [Fact]
    public async Task PostVehicle_UnknownInsurer_ReturnsErrorKeyedByDottedFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync(
            "/api/vehicles", VehicleBody("M-KF 3", new { vehicleInsurer = "NOPE" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"Commercial.VehicleInsurer\"");
    }

    [Fact]
    public async Task DeleteInsurer_InUse_ReturnsConflict()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleCatalogEntryResponse insurer = await CreateInsurerAsync(admin, "USED-INS");
        (await admin.PostAsJsonAsync("/api/vehicles", VehicleBody("M-KF 4", new { transportInsurer = "USED-INS" })))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await admin.DeleteAsync($"/api/vehicle-catalog/InsuranceCompany/{insurer.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetDocumentTypes_FreshDatabase_ReturnsTheSeededTypes()
    {
        VehicleCatalogEntryResponse[] entries = (await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<VehicleCatalogEntryResponse[]>("/api/vehicle-catalog/DocumentType"))!;

        entries.Select(e => e.Code).Should().Contain(["ADR", "ATP", "CEMT", "Tachograph", "UVV", "Crane", "TailLift"]);
        entries.Should().AllSatisfy(e => e.Kind.Should().Be("DocumentType"));
    }

    [Fact]
    public async Task Documents_FullLifecycle_AddListUpdateAndDelete()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-SP 1");
        string route = $"/api/vehicles/{vehicle.Id}/documents";

        HttpResponseMessage created = await admin.PostAsJsonAsync(route, new
        {
            documentType = "adr", lastCheckedOn = "2024-01-31", validUntil = "2025-01-31", remark = "Erstprüfung"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleDocumentResponse adr = (await created.Content.ReadFromJsonAsync<VehicleDocumentResponse>())!;
        created.Headers.Location!.ToString().Should().Be($"{route}/{adr.Id}");
        adr.DocumentType.Should().Be("ADR");
        adr.ModifiedBy.Should().NotBeNullOrEmpty();
        (await admin.PostAsJsonAsync(route, new { documentType = "UVV" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync(route, new { documentType = "Tachograph", validUntil = "2024-06-30" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        VehicleDocumentResponse[] listed = (await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<VehicleDocumentResponse[]>(route))!;
        listed.Select(d => d.DocumentType).Should().Equal("Tachograph", "ADR", "UVV");

        HttpResponseMessage put = await admin.PutAsJsonAsync($"{route}/{adr.Id}", new
        {
            documentType = "ADR", lastCheckedOn = "2025-01-15", validUntil = "2030-01-31", remark = (string?)null
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleDocumentResponse updated = (await put.Content.ReadFromJsonAsync<VehicleDocumentResponse>())!;
        updated.ValidUntil.Should().Be(new DateOnly(2030, 1, 31));
        updated.Remark.Should().BeNull();

        (await admin.DeleteAsync($"{route}/{adr.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"{route}/{adr.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<VehicleDocumentResponse[]>(route))!.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostDocument_DuplicateType_ReturnsConflict()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-SP 2");
        string route = $"/api/vehicles/{vehicle.Id}/documents";
        await admin.PostAsJsonAsync(route, new { documentType = "ATP" });

        HttpResponseMessage response = await admin.PostAsJsonAsync(route, new { documentType = "atp" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostDocument_InvalidFields_ReturnsErrorsKeyedByField()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-SP 3");

        HttpResponseMessage invalid = await admin.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/documents", new
        {
            documentType = "ADR", lastCheckedOn = "2025-02-01", validUntil = "2025-01-01"
        });
        HttpResponseMessage unknownType = await admin.PostAsJsonAsync(
            $"/api/vehicles/{vehicle.Id}/documents", new { documentType = "NOPE" });

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadAsStringAsync()).Should().Contain("\"ValidUntil\"");
        unknownType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownType.Content.ReadAsStringAsync()).Should().Contain("\"DocumentType\"");
    }

    [Fact]
    public async Task Documents_UnknownVehicle_ReturnsNotFound()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        string route = $"/api/vehicles/{Guid.CreateVersion7()}/documents";

        (await admin.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync(route, new { documentType = "ADR" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostDocument_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").PostAsJsonAsync(
            $"/api/vehicles/{Guid.CreateVersion7()}/documents", new { documentType = "ADR" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteVehicle_WithDocuments_RemovesThemAndBlocksTheirTypeNoLonger()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-SP 4");
        await admin.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/documents", new { documentType = "CEMT" });
        VehicleCatalogEntryResponse cemt = (await admin
                .GetFromJsonAsync<VehicleCatalogEntryResponse[]>("/api/vehicle-catalog/DocumentType"))!
            .Single(e => e.Code == "CEMT");
        cemt.UsageCount.Should().BeGreaterThanOrEqualTo(1);

        (await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.GetAsync($"/api/vehicles/{vehicle.Id}/documents")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        VehicleCatalogEntryResponse after = (await admin
                .GetFromJsonAsync<VehicleCatalogEntryResponse[]>("/api/vehicle-catalog/DocumentType"))!
            .Single(e => e.Code == "CEMT");
        after.UsageCount.Should().Be(cemt.UsageCount - 1);
    }
}
