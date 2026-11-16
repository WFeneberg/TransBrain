using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances;

namespace TransBrain.Api.IntegrationTests;

public class VehicleMaintenanceEndpointsTests(TransBrainApiFactory factory)
    : IClassFixture<TransBrainApiFactory>
{
    private static object VehicleBody(string plate) => new
    {
        licensePlate = plate,
        type = "Tractor",
        payloadKg = 24_000,
        loadMeters = 13.6m,
        nextInspectionDue = "2027-03-31",
    };

    private async Task<VehicleResponse> CreateVehicleAsync(HttpClient admin, string plate)
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/vehicles", VehicleBody(plate));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    [Fact]
    public async Task Maintenances_FullLifecycle_AddListUpdateAndDelete()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-WA 1");
        string route = $"/api/vehicles/{vehicle.Id}/maintenances";

        HttpResponseMessage created = await admin.PostAsJsonAsync(route, new
        {
            kind = "service",
            performedOn = "2026-05-10",
            description = "Ölwechsel und Filter",
            workshop = "Werkstatt Nord",
            odometerKm = 250_000,
            cost = 849.90m,
            nextDueOn = "2027-05-10",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        VehicleMaintenanceResponse service = (await created.Content.ReadFromJsonAsync<VehicleMaintenanceResponse>())!;
        created.Headers.Location!.ToString().Should().Be($"{route}/{service.Id}");
        service.Kind.Should().Be("Service");
        service.Description.Should().Be("Ölwechsel und Filter");
        service.Cost.Should().Be(849.90m);
        service.ModifiedBy.Should().NotBeNullOrEmpty();

        (await admin.PostAsJsonAsync(route, new
        {
            kind = "Repair", performedOn = "2026-08-01", description = "Bremsen erneuert"
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsJsonAsync(route, new
        {
            kind = "Inspection", performedOn = "2026-02-15", description = "HU/AU"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        VehicleMaintenanceResponse[] listed = (await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<VehicleMaintenanceResponse[]>(route))!;
        listed.Select(m => m.Kind).Should().Equal("Repair", "Service", "Inspection");

        HttpResponseMessage put = await admin.PutAsJsonAsync($"{route}/{service.Id}", new
        {
            kind = "Service",
            performedOn = "2026-05-11",
            description = "Ölwechsel, Filter und Bremsflüssigkeit",
            workshop = (string?)null,
            odometerKm = 250_100,
            cost = 999.00m,
            nextDueOn = (string?)null,
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        VehicleMaintenanceResponse updated = (await put.Content.ReadFromJsonAsync<VehicleMaintenanceResponse>())!;
        updated.PerformedOn.Should().Be(new DateOnly(2026, 5, 11));
        updated.Workshop.Should().BeNull();
        updated.NextDueOn.Should().BeNull();
        updated.Cost.Should().Be(999.00m);

        (await admin.DeleteAsync($"{route}/{service.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"{route}/{service.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<VehicleMaintenanceResponse[]>(route))!.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostMaintenance_InvalidFields_ReturnsErrorsKeyedByField()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-WA 2");
        string route = $"/api/vehicles/{vehicle.Id}/maintenances";

        HttpResponseMessage invalid = await admin.PostAsJsonAsync(route, new
        {
            kind = "Service", performedOn = "2026-05-10", description = "x", nextDueOn = "2026-05-09"
        });
        HttpResponseMessage unknownKind = await admin.PostAsJsonAsync(route, new
        {
            kind = "Lackieren", performedOn = "2026-05-10", description = "x"
        });

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadAsStringAsync()).Should().Contain("\"NextDueOn\"");
        unknownKind.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownKind.Content.ReadAsStringAsync()).Should().Contain("\"Kind\"");
    }

    [Fact]
    public async Task Maintenances_UnknownVehicle_ReturnsNotFound()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        string route = $"/api/vehicles/{Guid.CreateVersion7()}/maintenances";

        (await admin.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync(route, new
        {
            kind = "Service", performedOn = "2026-05-10", description = "x"
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostMaintenance_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").PostAsJsonAsync(
            $"/api/vehicles/{Guid.CreateVersion7()}/maintenances", new
            {
                kind = "Service", performedOn = "2026-05-10", description = "x"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteVehicle_WithMaintenances_RemovesThem()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        VehicleResponse vehicle = await CreateVehicleAsync(admin, "M-WA 3");
        (await admin.PostAsJsonAsync($"/api/vehicles/{vehicle.Id}/maintenances", new
        {
            kind = "Other", performedOn = "2026-01-01", description = "Garantiearbeiten"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.GetAsync($"/api/vehicles/{vehicle.Id}/maintenances"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
