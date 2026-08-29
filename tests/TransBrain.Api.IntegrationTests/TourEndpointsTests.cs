using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Vehicles;

namespace TransBrain.Api.IntegrationTests;

public class TourEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    // Tour dates are unique per test on purpose: the (tour_date, vehicle_id) and
    // (tour_date, driver_id) unique indexes are global, so two unrelated tests sharing a date
    // would collide through the database rather than through anything either test is about.
    private static int _plateCounter;

    private static string NextPlate() => $"M-TE {2000 + Interlocked.Increment(ref _plateCounter)}";

    private async Task<VehicleResponse> CreateVehicleAsync(int payloadKg = 18_000, decimal loadMeters = 13.6m)
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/vehicles", new
        {
            licensePlate = NextPlate(),
            type = "RigidTruck",
            payloadKg,
            loadMeters,
            nextInspectionDue = "2028-03-31"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    private async Task<DriverResponse> CreateDriverAsync(string lastName, string? externalUserId = null)
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName,
            licenseClasses = new[] { "CE" },
            licenseValidUntil = "2099-06-30",
            externalUserId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DriverResponse>())!;
    }

    private async Task<OrderResponse> CreateOrderAsync(int weightKg = 5_000, decimal loadMeters = 4.0m)
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync("/api/orders", new
        {
            consignor = new
            {
                name = "Absender GmbH", street = "Hauptstr. 1", postalCode = "80331",
                city = "Muenchen", country = "DE"
            },
            consignee = new
            {
                name = "Empfaenger AG", street = "Bahnhofstr. 2", postalCode = "10115",
                city = "Berlin", country = "DE"
            },
            cargoDescription = "Palettenware",
            cargoWeightKg = weightKg,
            cargoLoadMeters = loadMeters,
            pickupFrom = "2027-03-01T08:00:00+00:00",
            pickupTo = "2027-03-01T10:00:00+00:00",
            deliveryFrom = "2027-03-01T12:00:00+00:00",
            deliveryTo = "2027-03-01T16:00:00+00:00"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    private async Task<TourResponse> CreateTourAsync(DateOnly date, Guid vehicleId, Guid driverId)
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/tours", new { tourDate = date.ToString("yyyy-MM-dd"), vehicleId, driverId });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<TourResponse>())!;
    }

    [Fact]
    public async Task PostTour_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsJsonAsync("/api/tours", new
        {
            tourDate = "2098-01-01",
            vehicleId = Guid.CreateVersion7(),
            driverId = Guid.CreateVersion7()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTour_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer").PostAsJsonAsync("/api/tours", new
        {
            tourDate = "2098-01-02",
            vehicleId = Guid.CreateVersion7(),
            driverId = Guid.CreateVersion7()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostTour_AsDisponent_ReturnsCreatedAndIsListable()
    {
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("TourListbar");
        DateOnly date = new(2098, 1, 3);

        TourResponse tour = await CreateTourAsync(date, vehicle.Id, driver.Id);

        tour.Status.Should().Be("Planned");
        tour.Stops.Should().BeEmpty();

        HttpResponseMessage list = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/tours?tourDate={date:yyyy-MM-dd}");
        PagedResult<TourResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<TourResponse>>();

        page!.Items.Should().Contain(t => t.Id == tour.Id);
    }

    [Fact]
    public async Task PostTour_SameVehicleAndDateTwice_ReturnsConflict()
    {
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse first = await CreateDriverAsync("DoppeltHttpEins");
        DriverResponse second = await CreateDriverAsync("DoppeltHttpZwei");
        DateOnly date = new(2098, 1, 4);

        await CreateTourAsync(date, vehicle.Id, first.Id);

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync(
            "/api/tours",
            new { tourDate = date.ToString("yyyy-MM-dd"), vehicleId = vehicle.Id, driverId = second.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Tour.VehicleAlreadyBooked");
    }

    [Fact]
    public async Task PostTour_UnavailableDriverLicence_ReturnsConflict()
    {
        VehicleResponse vehicle = await CreateVehicleAsync();

        HttpResponseMessage created = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "AbgelaufenerSchein",
            licenseClasses = new[] { "CE" },
            licenseValidUntil = "2027-01-01",
            externalUserId = (string?)null
        });
        DriverResponse driver = (await created.Content.ReadFromJsonAsync<DriverResponse>())!;

        HttpResponseMessage response = await factory.CreateClientAs("disponent").PostAsJsonAsync(
            "/api/tours",
            new { tourDate = "2098-01-05", vehicleId = vehicle.Id, driverId = driver.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Tour.LicenceExpired");
    }

    [Fact]
    public async Task GetTourById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/tours/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostOrders_AssignsAnOrderAndReportsCapacity()
    {
        VehicleResponse vehicle = await CreateVehicleAsync(payloadKg: 10_000, loadMeters: 10.0m);
        DriverResponse driver = await CreateDriverAsync("Zuordnung");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 2, 1), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync(weightKg: 4_000, loadMeters: 3.0m);

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TourResponse updated = (await response.Content.ReadFromJsonAsync<TourResponse>())!;
        updated.Stops.Should().HaveCount(2);
        updated.Stops[0].StopType.Should().Be("Pickup");
        updated.Stops[0].OrderNumber.Should().Be(order.OrderNumber);
        updated.Stops[1].StopType.Should().Be("Delivery");
        updated.TotalWeightKg.Should().Be(4_000);
        updated.VehiclePayloadKg.Should().Be(10_000);
        updated.TotalLoadMeters.Should().Be(3.0m);
        updated.VehicleLoadMeters.Should().Be(10.0m);
    }

    [Fact]
    public async Task PostOrders_OrderTooHeavyForTheVehicle_ReturnsConflict()
    {
        VehicleResponse vehicle = await CreateVehicleAsync(payloadKg: 3_000, loadMeters: 10.0m);
        DriverResponse driver = await CreateDriverAsync("ZuSchwer");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 2, 2), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync(weightKg: 9_000);

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Tour.PayloadExceeded");
    }

    [Fact]
    public async Task DeleteOrder_RemovesTheStopsAndReturnsTheOrderToDraft()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("Entfernung");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 2, 3), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync();

        await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        HttpResponseMessage response = await dispatcher.DeleteAsync($"/api/tours/{tour.Id}/orders/{order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TourResponse updated = (await response.Content.ReadFromJsonAsync<TourResponse>())!;
        updated.Stops.Should().BeEmpty();
        updated.TotalWeightKg.Should().Be(0);

        // The order must be assignable again, which means Draft rather than stranded in Planned.
        HttpResponseMessage reread = await dispatcher.GetAsync($"/api/orders/{order.Id}");
        OrderResponse afterRemoval = (await reread.Content.ReadFromJsonAsync<OrderResponse>())!;
        afterRemoval.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task PostStart_AsDisponent_MovesTheTourAndItsOrders()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("Start");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 3, 1), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync();
        await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        HttpResponseMessage response = await dispatcher.PostAsync($"/api/tours/{tour.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TourResponse started = (await response.Content.ReadFromJsonAsync<TourResponse>())!;
        started.Status.Should().Be("InProgress");

        HttpResponseMessage reread = await dispatcher.GetAsync($"/api/orders/{order.Id}");
        OrderResponse moved = (await reread.Content.ReadFromJsonAsync<OrderResponse>())!;
        moved.Status.Should().Be("InTransit");
    }

    [Fact]
    public async Task PostComplete_AfterStart_DeliversTheOrders()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("Abschluss");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 3, 2), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync();
        await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });
        await dispatcher.PostAsync($"/api/tours/{tour.Id}/start", null);

        HttpResponseMessage response = await dispatcher.PostAsync($"/api/tours/{tour.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TourResponse completed = (await response.Content.ReadFromJsonAsync<TourResponse>())!;
        completed.Status.Should().Be("Completed");

        HttpResponseMessage reread = await dispatcher.GetAsync($"/api/orders/{order.Id}");
        OrderResponse delivered = (await reread.Content.ReadFromJsonAsync<OrderResponse>())!;
        delivered.Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task PostStart_AsTheAssignedDriver_Succeeds()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("EigeneTour", "driver-sub-own");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 4, 1), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync();
        await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        HttpResponseMessage response = await factory.CreateClientAsSubject("driver-sub-own", "fahrer")
            .PostAsync($"/api/tours/{tour.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // This is the test that proves spec §9's "nur eigene" over HTTP: the fahrer role passes the
    // TourStatusWrite policy, and the refusal comes from the handler, not the policy.
    [Fact]
    public async Task PostStart_AsAForeignDriver_ReturnsForbidden()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("FremdeTour", "driver-sub-owner");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 4, 2), vehicle.Id, driver.Id);
        OrderResponse order = await CreateOrderAsync();
        await dispatcher.PostAsJsonAsync($"/api/tours/{tour.Id}/orders", new { transportOrderId = order.Id });

        HttpResponseMessage response = await factory.CreateClientAsSubject("driver-sub-intruder", "fahrer")
            .PostAsync($"/api/tours/{tour.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Tour.NotYours");

        HttpResponseMessage reread = await dispatcher.GetAsync($"/api/tours/{tour.Id}");
        TourResponse unchanged = (await reread.Content.ReadFromJsonAsync<TourResponse>())!;
        unchanged.Status.Should().Be("Planned");
    }

    [Fact]
    public async Task GetTours_AsADriver_ListsOnlyTheirOwn()
    {
        VehicleResponse mineVehicle = await CreateVehicleAsync();
        VehicleResponse otherVehicle = await CreateVehicleAsync();
        DriverResponse mine = await CreateDriverAsync("ListeEigen", "driver-sub-list");
        DriverResponse other = await CreateDriverAsync("ListeFremd", "driver-sub-list-other");
        DateOnly date = new(2098, 5, 1);
        TourResponse myTour = await CreateTourAsync(date, mineVehicle.Id, mine.Id);
        TourResponse otherTour = await CreateTourAsync(date, otherVehicle.Id, other.Id);

        HttpResponseMessage response = await factory.CreateClientAsSubject("driver-sub-list", "fahrer")
            .GetAsync($"/api/tours?tourDate={date:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<TourResponse>? page = await response.Content.ReadFromJsonAsync<PagedResult<TourResponse>>();
        page!.Items.Should().ContainSingle().Which.Id.Should().Be(myTour.Id);
        page.Items.Should().NotContain(t => t.Id == otherTour.Id);
    }

    [Fact]
    public async Task GetTourById_AsAForeignDriver_ReturnsForbidden()
    {
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("DetailFremd", "driver-sub-detail");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 5, 2), vehicle.Id, driver.Id);

        HttpResponseMessage response = await factory.CreateClientAsSubject("driver-sub-detail-other", "fahrer")
            .GetAsync($"/api/tours/{tour.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostStart_AsViewer_ReturnsForbidden()
    {
        VehicleResponse vehicle = await CreateVehicleAsync();
        DriverResponse driver = await CreateDriverAsync("ViewerDarfNicht");
        TourResponse tour = await CreateTourAsync(new DateOnly(2098, 5, 3), vehicle.Id, driver.Id);

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsync($"/api/tours/{tour.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
