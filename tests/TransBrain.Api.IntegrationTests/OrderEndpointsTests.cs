using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Orders;

namespace TransBrain.Api.IntegrationTests;

public class OrderEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object AnAddress(string name) => new
    {
        name,
        street = "Hauptstr. 1",
        postalCode = "80331",
        city = "München",
        country = "DE"
    };

    private static object NewOrder(string consignorName) => new
    {
        consignor = AnAddress(consignorName),
        consignee = AnAddress("Empfänger AG"),
        cargoDescription = "Palettenware",
        cargoWeightKg = 12_000,
        cargoLoadMeters = 8.4m,
        pickupFrom = "2027-03-01T08:00:00+00:00",
        pickupTo = "2027-03-01T10:00:00+00:00",
        deliveryFrom = "2027-03-01T12:00:00+00:00",
        deliveryTo = "2027-03-01T16:00:00+00:00"
    };

    private static object UpdatedOrder(string consignorName) => new
    {
        consignor = AnAddress(consignorName),
        consignee = AnAddress("Empfänger AG"),
        cargoDescription = "Kühlware",
        cargoWeightKg = 9_000,
        cargoLoadMeters = 6.2m,
        pickupFrom = "2027-04-01T08:00:00+00:00",
        pickupTo = "2027-04-01T10:00:00+00:00",
        deliveryFrom = "2027-04-01T12:00:00+00:00",
        deliveryTo = "2027-04-01T16:00:00+00:00"
    };

    private static async Task<OrderResponse> CreateOrderAs(
        TransBrainApiFactory factory, string role, string consignorName)
    {
        HttpResponseMessage created = await factory.CreateClientAs(role)
            .PostAsJsonAsync("/api/orders", NewOrder(consignorName));

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await created.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    [Fact]
    public async Task PostOrder_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/orders", NewOrder("Anon GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostOrder_AsViewer_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsJsonAsync("/api/orders", NewOrder("Viewer GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Orders are dispatch data, not master data: a dispatcher who cannot create an order cannot
    // do their job. This is the test that pins the DispatchWrite policy choice.
    [Fact]
    public async Task PostOrder_AsDisponent_ReturnsCreatedAndIsListable()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Dispo Listable GmbH");

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/orders?pageSize=100");
        PagedResult<OrderResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<OrderResponse>>();

        page!.Items.Should().Contain(o => o.Id == order.Id && o.Consignor.Name == "Dispo Listable GmbH");
    }

    [Fact]
    public async Task PostOrder_AsAdmin_ReturnsCreated()
    {
        OrderResponse order = await CreateOrderAs(factory, "admin", "Admin GmbH");

        order.OrderNumber.Should().StartWith("TB-");
        order.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task GetOrderById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/orders/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutOrder_AsDisponent_UpdatesADraftOrder()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Vor Aenderung GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PutAsJsonAsync($"/api/orders/{order.Id}", UpdatedOrder("Nach Aenderung GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderResponse? updated = await response.Content.ReadFromJsonAsync<OrderResponse>();
        updated!.Consignor.Name.Should().Be("Nach Aenderung GmbH");
        updated.CargoDescription.Should().Be("Kühlware");
        updated.OrderNumber.Should().Be(order.OrderNumber);
    }

    [Fact]
    public async Task PutOrder_OnACancelledOrder_ReturnsConflict()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Storniert GmbH");

        HttpResponseMessage cancelled = await dispatcher.PostAsync($"/api/orders/{order.Id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await dispatcher
            .PutAsJsonAsync($"/api/orders/{order.Id}", UpdatedOrder("Zu spaet GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostCancel_AsDisponent_ReturnsTheCancelledOrder()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Zu stornieren GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsync($"/api/orders/{order.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderResponse? cancelled = await response.Content.ReadFromJsonAsync<OrderResponse>();
        cancelled!.Status.Should().Be("Cancelled");
        cancelled.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task PostCancel_Twice_ReturnsConflict()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Doppelt storniert GmbH");

        HttpResponseMessage first = await dispatcher.PostAsync($"/api/orders/{order.Id}/cancel", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage second = await dispatcher.PostAsync($"/api/orders/{order.Id}/cancel", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostCancel_AsViewer_ReturnsForbidden()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Viewer darf nicht GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsync($"/api/orders/{order.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostOrder_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/orders", new
            {
                consignor = AnAddress("   "),
                consignee = AnAddress("Empfänger AG"),
                cargoDescription = "",
                cargoWeightKg = 12_000,
                cargoLoadMeters = 8.4m,
                pickupFrom = "2027-03-01T08:00:00+00:00",
                pickupTo = "2027-03-01T10:00:00+00:00",
                deliveryFrom = "2027-03-01T12:00:00+00:00",
                deliveryTo = "2027-03-01T16:00:00+00:00"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Consignor.Name").And.Contain("CargoDescription");
    }

    // A same-site shipment - one works to another - is a legitimate order, and the two owned
    // Address values are then equal. Pinned because EF refuses two owned navigations backed by
    // the SAME instance; the handler calls Address.Create twice and so produces two distinct
    // instances, but nothing in the type system says it must, and this is what would break.
    [Fact]
    public async Task PostOrder_ConsignorAndConsigneeIdentical_ReturnsCreated()
    {
        object address = AnAddress("Werk Nord");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/orders", new
            {
                consignor = address,
                consignee = address,
                cargoDescription = "Werksverkehr",
                cargoWeightKg = 1_000,
                cargoLoadMeters = 1.5m,
                pickupFrom = "2027-03-01T08:00:00+00:00",
                pickupTo = "2027-03-01T10:00:00+00:00",
                deliveryFrom = "2027-03-01T12:00:00+00:00",
                deliveryTo = "2027-03-01T16:00:00+00:00"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        OrderResponse? order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Consignor.Name.Should().Be("Werk Nord");
        order.Consignee.Name.Should().Be("Werk Nord");
    }
}
