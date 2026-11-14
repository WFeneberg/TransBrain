using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The global Wartung (maintenance) area end to end: only system administrators may reach it —
/// every tenant role including admin is refused — a backup is a complete downloadable snapshot
/// of the tenant chosen in the route, and a restore replaces exactly that tenant's data,
/// atomically, without leaking into or reading from any other tenant.
/// </summary>
public sealed class SystemMaintenanceEndpointsTests(TransBrainApiFactory factory)
    : IClassFixture<TransBrainApiFactory>
{
    private static string BackupUrl(Guid tenant) => $"/api/system/tenants/{tenant}/backup";

    private static string RestoreUrl(Guid tenant) => $"/api/system/tenants/{tenant}/restore";

    private static object Vehicle(string licensePlate) => new
    {
        licensePlate,
        type = "Tractor",
        payloadKg = 24_000,
        loadMeters = 13.6m,
        nextInspectionDue = "2027-03-31"
    };

    private static readonly object Order = new
    {
        consignor = new
        {
            name = "Backup GmbH",
            street = "Lagerstraße 1",
            postalCode = "80331",
            city = "München",
            country = "DE"
        },
        consignee = new
        {
            name = "Restore AG",
            street = "Zielweg 2",
            postalCode = "20095",
            city = "Hamburg",
            country = "DE"
        },
        cargoDescription = "Palettenware",
        cargoWeightKg = 1_000,
        cargoLoadMeters = 2.0m,
        pickupFrom = "2027-03-01T08:00:00+00:00",
        pickupTo = "2027-03-01T10:00:00+00:00",
        deliveryFrom = "2027-03-01T12:00:00+00:00",
        deliveryTo = "2027-03-01T16:00:00+00:00"
    };

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task GetTenants_TenantRole_ReturnsForbidden(string role)
    {
        HttpResponseMessage response = await factory.CreateClientAs(role).GetAsync("/api/system/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task GetBackup_TenantRole_ReturnsForbidden(string role)
    {
        HttpResponseMessage response = await factory.CreateClientAs(role)
            .GetAsync(BackupUrl(Guid.CreateVersion7()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task PostRestore_TenantRole_ReturnsForbidden(string role)
    {
        HttpResponseMessage response = await factory.CreateClientAs(role)
            .PostAsJsonAsync(RestoreUrl(Guid.CreateVersion7()), new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBackup_EmptyTenantIdInRoute_ReturnsBadRequest()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient()
            .GetAsync(BackupUrl(Guid.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SystemTenant.Invalid");
    }

    [Fact]
    public async Task GetTenants_SystemAdminWithoutTenantClaim_ListsTenantsWithCounts()
    {
        Guid tenant = Guid.CreateVersion7();
        HttpClient admin = factory.CreateClientForTenant(tenant, "admin");
        await admin.PostAsJsonAsync("/api/vehicles", Vehicle("M-LST 1"));
        await admin.PostAsJsonAsync("/api/vehicles", Vehicle("M-LST 2"));

        HttpResponseMessage response = await factory.CreateSystemAdminClient().GetAsync("/api/system/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement tenants = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement entry = tenants.EnumerateArray()
            .Single(t => t.GetProperty("tenantId").GetGuid() == tenant);
        entry.GetProperty("vehicles").GetInt32().Should().Be(2);
        entry.GetProperty("totalRows").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetBackup_SystemAdminForTenantWithData_ReturnsCompleteDownloadableSnapshot()
    {
        Guid tenant = Guid.CreateVersion7();
        HttpClient admin = factory.CreateClientForTenant(tenant, "admin");
        await admin.PostAsJsonAsync("/api/vehicles", Vehicle("M-BK 100"));

        HttpResponseMessage response = await factory.CreateSystemAdminClient().GetAsync(BackupUrl(tenant));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName.Should().StartWith("transbrain-backup-");

        JsonElement backup = await response.Content.ReadFromJsonAsync<JsonElement>();
        backup.GetProperty("fileFormat").GetString().Should().Be("transbrain-tenant-backup");
        backup.GetProperty("formatVersion").GetInt32().Should().Be(1);
        backup.GetProperty("tenantId").GetGuid().Should().Be(tenant);
        backup.GetProperty("schemaVersion").GetString().Should().NotBeNullOrWhiteSpace();

        JsonElement tables = backup.GetProperty("tables");
        tables.GetProperty("vehicles").GetArrayLength().Should().Be(1);
        // Every tenant-owned table travels with the file, even when empty — a backup that
        // silently skipped tables could never prove it is complete.
        foreach (string table in new[] { "drivers", "customers", "tours", "tour_stops", "transport_orders", "invoices" })
        {
            tables.TryGetProperty(table, out JsonElement rows).Should().BeTrue($"table '{table}' must be part of the backup");
            rows.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task PostRestore_BackupTakenEarlier_RestoresExactlyThatState()
    {
        Guid tenant = Guid.CreateVersion7();
        HttpClient system = factory.CreateSystemAdminClient();
        HttpClient admin = factory.CreateClientForTenant(tenant, "admin");
        HttpClient dispatcher = factory.CreateClientForTenant(tenant, "disponent", "admin");

        // A small but deep data set: master data, an order, a tour with a stop (an owned table
        // with a database-generated key — the hardest row kind to restore faithfully).
        HttpResponseMessage vehicleCreated = await admin.PostAsJsonAsync("/api/vehicles", Vehicle("M-RST 1"));
        Guid vehicleId = (await vehicleCreated.Content.ReadFromJsonAsync<VehicleResponse>())!.Id;
        DriverResponse driver = (await (await admin.PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Rita",
            lastName = "Restore",
            licenseClasses = new[] { "CE" },
            licenseValidUntil = "2099-06-30",
            externalUserId = (string?)null
        })).Content.ReadFromJsonAsync<DriverResponse>())!;
        JsonElement order = await (await dispatcher.PostAsJsonAsync("/api/orders", Order))
            .Content.ReadFromJsonAsync<JsonElement>();
        Guid orderId = order.GetProperty("id").GetGuid();
        string orderNumber = order.GetProperty("orderNumber").GetString()!;
        TourResponse tour = (await (await dispatcher.PostAsJsonAsync("/api/tours", new
        {
            tourDate = "2098-01-05",
            vehicleId,
            driverId = driver.Id
        })).Content.ReadFromJsonAsync<TourResponse>())!;
        HttpResponseMessage assigned = await dispatcher.PostAsJsonAsync(
            $"/api/tours/{tour.Id}/orders", new { transportOrderId = orderId });
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);

        string backupJson = await (await system.GetAsync(BackupUrl(tenant))).Content.ReadAsStringAsync();

        // Diverge from the snapshot in both directions: lose rows (the tour's stops), gain a row.
        HttpResponseMessage unassigned = await dispatcher.DeleteAsync($"/api/tours/{tour.Id}/orders/{orderId}");
        unassigned.StatusCode.Should().Be(HttpStatusCode.OK);
        await admin.PostAsJsonAsync("/api/vehicles", Vehicle("M-RST 999"));

        HttpResponseMessage restore = await system.PostAsync(
            RestoreUrl(tenant), new StringContent(backupJson, Encoding.UTF8, "application/json"));

        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement result = await restore.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("totalRows").GetInt32().Should().BeGreaterThan(0);
        JsonElement counts = result.GetProperty("tables");
        counts.GetProperty("vehicles").GetInt32().Should().Be(1);
        counts.GetProperty("transport_orders").GetInt32().Should().Be(1);
        counts.GetProperty("tour_stops").GetInt32().Should().BeGreaterThan(0);

        // The post-backup vehicle is gone, the unassigned order is back on the tour with its
        // stop rows, and the order kept its number.
        JsonElement vehicles = await (await admin.GetAsync("/api/vehicles?pageSize=50"))
            .Content.ReadFromJsonAsync<JsonElement>();
        List<string?> plates = [.. vehicles.GetProperty("items").EnumerateArray()
            .Select(v => v.GetProperty("licensePlate").GetString())];
        plates.Should().Contain("M-RST 1").And.NotContain("M-RST 999");

        JsonElement restoredOrder = await (await admin.GetAsync($"/api/orders/{orderId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        restoredOrder.GetProperty("orderNumber").GetString().Should().Be(orderNumber);

        TourResponse? restoredTour = await (await dispatcher.GetAsync($"/api/tours/{tour.Id}"))
            .Content.ReadFromJsonAsync<TourResponse>();
        restoredTour!.Stops.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostRestore_AfterRestore_OrderNumberSequenceContinuesInsteadOfRestarting()
    {
        Guid tenant = Guid.CreateVersion7();
        HttpClient system = factory.CreateSystemAdminClient();
        HttpClient dispatcher = factory.CreateClientForTenant(tenant, "disponent", "admin");

        JsonElement first = await (await dispatcher.PostAsJsonAsync("/api/orders", Order))
            .Content.ReadFromJsonAsync<JsonElement>();
        first.GetProperty("orderNumber").GetString().Should().EndWith("-00001");

        string backupJson = await (await system.GetAsync(BackupUrl(tenant))).Content.ReadAsStringAsync();
        HttpResponseMessage restore = await system.PostAsync(
            RestoreUrl(tenant), new StringContent(backupJson, Encoding.UTF8, "application/json"));
        restore.StatusCode.Should().Be(HttpStatusCode.OK);

        // The number sequence travels with the backup: a fresh order must NOT reuse -00001,
        // or the tenant would mint duplicate order numbers after a restore.
        JsonElement second = await (await dispatcher.PostAsJsonAsync("/api/orders", Order))
            .Content.ReadFromJsonAsync<JsonElement>();
        second.GetProperty("orderNumber").GetString().Should().EndWith("-00002");
    }

    [Fact]
    public async Task PostRestore_BackupOfAnotherTenant_ReturnsBadRequestAndChangesNothing()
    {
        Guid ownerTenant = Guid.CreateVersion7();
        Guid victimTenant = Guid.CreateVersion7();
        HttpClient system = factory.CreateSystemAdminClient();
        HttpClient owner = factory.CreateClientForTenant(ownerTenant, "admin");
        HttpClient victim = factory.CreateClientForTenant(victimTenant, "admin");

        await owner.PostAsJsonAsync("/api/vehicles", Vehicle("M-OWN 1"));
        string foreignBackup = await (await system.GetAsync(BackupUrl(ownerTenant))).Content.ReadAsStringAsync();
        await victim.PostAsJsonAsync("/api/vehicles", Vehicle("M-VIC 1"));

        // The file was taken from ownerTenant, the route points at victimTenant: refused.
        HttpResponseMessage response = await system.PostAsync(
            RestoreUrl(victimTenant), new StringContent(foreignBackup, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Backup.TenantMismatch");

        JsonElement vehicles = await (await victim.GetAsync("/api/vehicles"))
            .Content.ReadFromJsonAsync<JsonElement>();
        vehicles.GetProperty("items").EnumerateArray()
            .Select(v => v.GetProperty("licensePlate").GetString())
            .Should().ContainSingle(p => p == "M-VIC 1");
    }

    [Fact]
    public async Task PostRestore_BackupFromDifferentSchemaVersion_ReturnsConflict()
    {
        Guid tenant = Guid.CreateVersion7();
        HttpClient system = factory.CreateSystemAdminClient();

        string backupJson = await (await system.GetAsync(BackupUrl(tenant))).Content.ReadAsStringAsync();
        JsonNode backup = JsonNode.Parse(backupJson)!;
        backup["schemaVersion"] = "19990101000000_SomeAncientSchema";

        HttpResponseMessage response = await system.PostAsync(
            RestoreUrl(tenant), new StringContent(backup.ToJsonString(), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Backup.SchemaMismatch");
    }

    [Fact]
    public async Task PostRestore_ArbitraryJsonInsteadOfBackup_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient().PostAsJsonAsync(
            RestoreUrl(Guid.CreateVersion7()), new { fileFormat = "not-a-backup", comment = "hello" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostRestore_ByOneTenant_LeavesOtherTenantsDataUntouched()
    {
        Guid restoringTenant = Guid.CreateVersion7();
        Guid bystanderTenant = Guid.CreateVersion7();
        HttpClient system = factory.CreateSystemAdminClient();
        HttpClient restorer = factory.CreateClientForTenant(restoringTenant, "admin");
        HttpClient bystander = factory.CreateClientForTenant(bystanderTenant, "admin");

        await restorer.PostAsJsonAsync("/api/vehicles", Vehicle("M-SELF 1"));
        await bystander.PostAsJsonAsync("/api/vehicles", Vehicle("M-BYST 1"));

        string backupJson = await (await system.GetAsync(BackupUrl(restoringTenant))).Content.ReadAsStringAsync();
        HttpResponseMessage restore = await system.PostAsync(
            RestoreUrl(restoringTenant), new StringContent(backupJson, Encoding.UTF8, "application/json"));
        restore.StatusCode.Should().Be(HttpStatusCode.OK);

        // The restore wipes and rewrites the tenant named in the route only; the bystander's
        // fleet must survive bit for bit.
        JsonElement vehicles = await (await bystander.GetAsync("/api/vehicles"))
            .Content.ReadFromJsonAsync<JsonElement>();
        vehicles.GetProperty("items").EnumerateArray()
            .Select(v => v.GetProperty("licensePlate").GetString())
            .Should().ContainSingle(p => p == "M-BYST 1");
    }
}
