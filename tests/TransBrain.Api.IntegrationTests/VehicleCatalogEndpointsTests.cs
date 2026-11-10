using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Api.IntegrationTests;

public class VehicleCatalogEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    [Fact]
    public async Task GetVehicleClasses_FreshDatabase_ReturnsTheSeededClassesOrderedByName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").GetAsync("/api/vehicle-catalog/vehicleclass");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleCatalogEntryResponse[] entries = (await response.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>())!;
        entries.Select(e => e.Code).Should().Contain(
        [
            "Tarpaulin", "Box", "Refrigerated", "Tank", "Silo", "Tipper", "Flatbed", "Container",
            "CarTransporter", "LowLoader", "Other",
        ]);
        entries.Single(e => e.Code == "Refrigerated").Name.Should().Be("Kühlkoffer");
        entries.Select(e => e.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
        entries.Should().AllSatisfy(e => e.Kind.Should().Be("VehicleClass"));
    }

    [Fact]
    public async Task GetCatalog_UnknownKind_ReturnsBadRequest()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").GetAsync("/api/vehicle-catalog/1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCatalog_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/vehicle-catalog/CarrierGroup");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostEntry_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/vehicle-catalog/CarrierGroup", new { code = "D1", name = "Dispo", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostEntry_InvalidFields_ReturnsErrorsKeyedCodeAndName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicle-catalog/CarrierGroup", new { code = "a b", name = "", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblem? problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors.Keys.Should().Contain(["Code", "Name"]);
    }

    [Fact]
    public async Task PostEntry_ExistingCodeInOtherCase_ReturnsConflict()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicle-catalog/VehicleClass", new { code = "TANK", name = "Tanker", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CarrierGroup_FullLifecycle_CreateUseDeactivateAndDelete()
    {
        HttpClient admin = factory.CreateClientAs("admin");

        HttpResponseMessage created = await admin.PostAsJsonAsync(
            "/api/vehicle-catalog/CarrierGroup", new { code = "FV-1", name = "Fernverkehr", isActive = true });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleCatalogEntryResponse entry = (await created.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse>())!;
        created.Headers.Location!.ToString().Should().Be($"/api/vehicle-catalog/CarrierGroup/{entry.Id}");

        HttpResponseMessage vehicleResponse = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-CG 1",
            type = "Tractor",
            payloadKg = 24_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2027-03-31",
            carrierGroup = "fv-1"
        });
        vehicleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleResponse vehicle = (await vehicleResponse.Content.ReadFromJsonAsync<VehicleResponse>())!;
        vehicle.CarrierGroup.Should().Be("FV-1");

        VehicleCatalogEntryResponse listed = await GetEntryAsync(admin, "CarrierGroup", entry.Id);
        listed.UsageCount.Should().Be(1);

        HttpResponseMessage blockedDelete = await admin.DeleteAsync($"/api/vehicle-catalog/CarrierGroup/{entry.Id}");
        blockedDelete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        HttpResponseMessage deactivated = await admin.PutAsJsonAsync(
            $"/api/vehicle-catalog/CarrierGroup/{entry.Id}", new { name = "Fernverkehr (alt)", isActive = false });
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleCatalogEntryResponse updated = (await deactivated.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse>())!;
        updated.Name.Should().Be("Fernverkehr (alt)");
        updated.IsActive.Should().BeFalse();
        updated.Code.Should().Be("FV-1");

        // The vehicle that already has the inactive group can still be saved; a new one cannot pick it.
        HttpResponseMessage keep = await admin.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", new
        {
            licensePlate = "M-CG 1",
            type = "Tractor",
            payloadKg = 25_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2027-03-31",
            carrierGroup = "FV-1"
        });
        keep.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage rejected = await admin.PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = "M-CG 2",
            type = "Tractor",
            payloadKg = 24_000,
            loadMeters = 13.6m,
            nextInspectionDue = "2027-03-31",
            carrierGroup = "FV-1"
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage deleted = await admin.DeleteAsync($"/api/vehicle-catalog/CarrierGroup/{entry.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage list = await admin.GetAsync("/api/vehicle-catalog/CarrierGroup");
        VehicleCatalogEntryResponse[] remaining = (await list.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>())!;
        remaining.Should().NotContain(e => e.Id == entry.Id);
    }

    [Fact]
    public async Task PutEntry_IdOfTheOtherKind_ReturnsNotFound()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleCatalogEntryResponse tank = (await (await admin.GetAsync("/api/vehicle-catalog/VehicleClass"))
            .Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>())!.Single(e => e.Code == "Tank");

        HttpResponseMessage response = await admin.PutAsJsonAsync(
            $"/api/vehicle-catalog/CarrierGroup/{tank.Id}", new { name = "X", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEntry_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .DeleteAsync($"/api/vehicle-catalog/VehicleClass/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<VehicleCatalogEntryResponse> GetEntryAsync(HttpClient client, string kind, Guid id)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/vehicle-catalog/{kind}");
        VehicleCatalogEntryResponse[] entries = (await response.Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>())!;
        return entries.Single(e => e.Id == id);
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
