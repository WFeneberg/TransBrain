using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// Proves the tenant (Mandant) row isolation end to end: data written by one tenant must be
/// unreachable for every other tenant through every read path — lists, lookups by id, and
/// uniqueness checks — and a caller without any tenant must be rejected before reaching data.
/// </summary>
public sealed class TenantIsolationTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static readonly Guid SecondTenant = Guid.Parse("0199c0de-0000-7000-8000-000000000002");

    private static object Vehicle(string licensePlate) => new
    {
        licensePlate,
        type = "Tractor",
        payloadKg = 24_000,
        loadMeters = 13.6m,
        nextInspectionDue = "2027-03-31"
    };

    [Fact]
    public async Task GetVehicles_CreatedByAnotherTenant_IsNeitherListedNorFetchableById()
    {
        HttpClient defaultTenantAdmin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await defaultTenantAdmin.PostAsJsonAsync("/api/vehicles", Vehicle("M-TEN 1"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid vehicleId = (await created.Content.ReadFromJsonAsync<VehicleResponse>())!.Id;

        HttpClient otherTenantAdmin = factory.CreateClientForTenant(SecondTenant, "admin");

        PagedResult<VehicleResponse>? page = await (await otherTenantAdmin.GetAsync("/api/vehicles"))
            .Content.ReadFromJsonAsync<PagedResult<VehicleResponse>>();
        page!.Items.Should().NotContain(v => v.LicensePlate == "M-TEN 1");

        // By id the foreign row must look nonexistent, not forbidden: a 403 would leak that the
        // id exists in some other tenant.
        HttpResponseMessage byId = await otherTenantAdmin.GetAsync($"/api/vehicles/{vehicleId}");
        byId.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostVehicle_SameLicensePlateInTwoTenants_BothSucceed()
    {
        HttpResponseMessage first = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/vehicles", Vehicle("M-TEN 22"));
        HttpResponseMessage second = await factory.CreateClientForTenant(SecondTenant, "admin")
            .PostAsJsonAsync("/api/vehicles", Vehicle("M-TEN 22"));

        // The license plate is only unique per tenant; two haulage firms may well run vehicles
        // with (historically reassigned) identical plates, and tenant B must never be blocked —
        // or probed — by tenant A's fleet.
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetVehicles_AuthenticatedWithoutTenantClaim_ReturnsForbidden()
    {
        HttpClient noTenant = factory.CreateClientAs("admin");
        noTenant.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, TestAuthHandler.NoTenant);

        HttpResponseMessage response = await noTenant.GetAsync("/api/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVehicleCatalog_SeededEntries_BelongOnlyToTheDefaultTenant()
    {
        VehicleCatalogEntryResponse[]? defaultTenantEntries =
            await (await factory.CreateClientAs("viewer").GetAsync("/api/vehicle-catalog/VehicleClass"))
                .Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>();
        defaultTenantEntries.Should().NotBeEmpty();

        // A brand-new tenant starts with an empty catalog and curates its own entries; the
        // seed data is historical default-tenant content, not shared master data.
        VehicleCatalogEntryResponse[]? otherTenantEntries =
            await (await factory.CreateClientForTenant(SecondTenant, "viewer").GetAsync("/api/vehicle-catalog/VehicleClass"))
                .Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>();
        otherTenantEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task PostTransportOrder_InTwoTenants_DrawsIndependentNumberSequences()
    {
        HttpClient defaultTenantDispo = factory.CreateClientAs("disponent", "admin");
        HttpClient otherTenantDispo = factory.CreateClientForTenant(SecondTenant, "disponent", "admin");

        string firstNumber = await CreateOrderAndReadNumber(defaultTenantDispo);
        string secondNumber = await CreateOrderAndReadNumber(otherTenantDispo);

        // Both tenants draw from their own per-year sequence, so a fresh tenant starts at -00001
        // regardless of how many orders other tenants have already created. Equal numbers across
        // tenants are fine (and expected here if the default tenant's first order is also -00001);
        // what matters is that the second tenant's counter is provably its own.
        secondNumber.Should().EndWith("-00001");
        firstNumber.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<string> CreateOrderAndReadNumber(HttpClient client)
    {
        object order = new
        {
            consignor = new
            {
                name = "Mandanten GmbH",
                street = "Lagerstraße 1",
                postalCode = "80331",
                city = "München",
                country = "DE"
            },
            consignee = new
            {
                name = "Empfänger AG",
                street = "Zielweg 2",
                postalCode = "20095",
                city = "Hamburg",
                country = "DE"
            },
            cargoDescription = "Palettenware",
            cargoWeightKg = 12_000,
            cargoLoadMeters = 8.4m,
            pickupFrom = "2027-03-01T08:00:00+00:00",
            pickupTo = "2027-03-01T10:00:00+00:00",
            deliveryFrom = "2027-03-01T12:00:00+00:00",
            deliveryTo = "2027-03-01T16:00:00+00:00"
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", order);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        System.Text.Json.JsonElement body =
            await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("orderNumber").GetString()!;
    }
}
