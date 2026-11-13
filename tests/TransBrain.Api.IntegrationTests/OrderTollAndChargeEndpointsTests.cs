using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.OrderCharges;
using TransBrain.Application.Features.Orders.OrderTolls;

namespace TransBrain.Api.IntegrationTests;

public class OrderTollAndChargeEndpointsTests(TransBrainApiFactory factory)
    : IClassFixture<TransBrainApiFactory>
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
        deliveryTo = "2027-03-01T16:00:00+00:00",
        notes = "Nur mit Hebebühne anliefern",
        confirmationNotes = "Anlieferung avisieren",
        requiresPartitionWall = true,
        requiresDoubleDeck = false
    };

    // "A1" and "Stk." are seeded legacy catalog entries (TollCategory / Unit).
    private static object NewToll() => new
    {
        tollCategory = "A1",
        emptyKm = 120,
        loadedKm = 480,
        factorPerKm = 0.187m
    };

    private static object NewCharge() => new
    {
        description = "Standgeld",
        quantity = 2.5m,
        unit = "Stk",
        unitPrice = 45.80m
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
    public async Task PostOrder_WithNotesAndFlags_RoundTripsThem()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Besonderes GmbH");

        order.Notes.Should().Be("Nur mit Hebebühne anliefern");
        order.ConfirmationNotes.Should().Be("Anlieferung avisieren");
        order.RequiresPartitionWall.Should().BeTrue();
        order.RequiresDoubleDeck.Should().BeFalse();
    }

    [Fact]
    public async Task PostToll_AsDisponent_ComputesTheAmountsFromTheKilometres()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Maut GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/orders/{order.Id}/tolls", NewToll());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        OrderTollResponse? toll = await response.Content.ReadFromJsonAsync<OrderTollResponse>();
        toll!.TollCategory.Should().Be("A1");
        toll.EmptyAmount.Should().Be(120 * 0.187m);
        toll.LoadedAmount.Should().Be(480 * 0.187m);
        toll.ModifiedBy.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TollRoundTrip_CreateUpdateDelete_WorksAndIsListable()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Maut Runde GmbH");

        HttpResponseMessage created = await dispatcher
            .PostAsJsonAsync($"/api/orders/{order.Id}/tolls", NewToll());
        OrderTollResponse toll = (await created.Content.ReadFromJsonAsync<OrderTollResponse>())!;

        HttpResponseMessage updated = await dispatcher.PutAsJsonAsync(
            $"/api/orders/{order.Id}/tolls/{toll.Id}",
            new { tollCategory = "A2", emptyKm = 0, loadedKm = 300, factorPerKm = 0.31m });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderTollResponse? afterUpdate = await updated.Content.ReadFromJsonAsync<OrderTollResponse>();
        afterUpdate!.TollCategory.Should().Be("A2");
        afterUpdate.LoadedAmount.Should().Be(300 * 0.31m);

        HttpResponseMessage list = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/orders/{order.Id}/tolls");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderTollResponse[]? tolls = await list.Content.ReadFromJsonAsync<OrderTollResponse[]>();
        tolls.Should().ContainSingle(t => t.Id == toll.Id);

        HttpResponseMessage deleted = await dispatcher
            .DeleteAsync($"/api/orders/{order.Id}/tolls/{toll.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        OrderTollResponse[]? after = await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<OrderTollResponse[]>($"/api/orders/{order.Id}/tolls");
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task PostToll_AsViewer_ReturnsForbidden()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Maut Viewer GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsJsonAsync($"/api/orders/{order.Id}/tolls", NewToll());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostToll_UnknownOrder_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/orders/{Guid.CreateVersion7()}/tolls", NewToll());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostToll_UnknownCategory_ReturnsBadRequestKeyedByField()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Maut Unbekannt GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/orders/{order.Id}/tolls", new
            {
                tollCategory = "GibtEsNicht",
                emptyKm = 10,
                loadedKm = 20,
                factorPerKm = 0.1m
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TollCategory");
    }

    // Toll costs become known while the tour is on the road or afterwards - unlike the
    // order header, toll records are deliberately editable in any status.
    [Fact]
    public async Task PostToll_OnACancelledOrder_StillReturnsCreated()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Maut Storno GmbH");

        HttpResponseMessage cancelled = await dispatcher.PostAsync($"/api/orders/{order.Id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await dispatcher
            .PostAsJsonAsync($"/api/orders/{order.Id}/tolls", NewToll());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostCharge_AsDisponent_ComputesTheTotal()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Zusatz GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/orders/{order.Id}/charges", NewCharge());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        OrderChargeResponse? charge = await response.Content.ReadFromJsonAsync<OrderChargeResponse>();
        charge!.Description.Should().Be("Standgeld");
        charge.Unit.Should().Be("Stk");
        charge.Total.Should().Be(2.5m * 45.80m);
    }

    [Fact]
    public async Task ChargeRoundTrip_CreateUpdateDelete_WorksAndIsListable()
    {
        HttpClient dispatcher = factory.CreateClientAs("disponent");
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Zusatz Runde GmbH");

        HttpResponseMessage created = await dispatcher
            .PostAsJsonAsync($"/api/orders/{order.Id}/charges", NewCharge());
        OrderChargeResponse charge = (await created.Content.ReadFromJsonAsync<OrderChargeResponse>())!;

        // A charge without a unit is legitimate - the legacy grid left the column empty too.
        HttpResponseMessage updated = await dispatcher.PutAsJsonAsync(
            $"/api/orders/{order.Id}/charges/{charge.Id}",
            new { description = "Palettentausch", quantity = 4m, unit = (string?)null, unitPrice = 12m });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderChargeResponse? afterUpdate = await updated.Content.ReadFromJsonAsync<OrderChargeResponse>();
        afterUpdate!.Unit.Should().BeNull();
        afterUpdate.Total.Should().Be(48m);

        HttpResponseMessage list = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/orders/{order.Id}/charges");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderChargeResponse[]? charges = await list.Content.ReadFromJsonAsync<OrderChargeResponse[]>();
        charges.Should().ContainSingle(c => c.Id == charge.Id);

        HttpResponseMessage deleted = await dispatcher
            .DeleteAsync($"/api/orders/{order.Id}/charges/{charge.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        OrderChargeResponse[]? after = await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<OrderChargeResponse[]>($"/api/orders/{order.Id}/charges");
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task PostCharge_AsViewer_ReturnsForbidden()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Zusatz Viewer GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsJsonAsync($"/api/orders/{order.Id}/charges", NewCharge());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostCharge_UnknownUnit_ReturnsBadRequestKeyedByField()
    {
        OrderResponse order = await CreateOrderAs(factory, "disponent", "Zusatz Unbekannt GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/orders/{order.Id}/charges", new
            {
                description = "Standgeld",
                quantity = 1m,
                unit = "GibtEsNicht",
                unitPrice = 10m
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unit");
    }
}
