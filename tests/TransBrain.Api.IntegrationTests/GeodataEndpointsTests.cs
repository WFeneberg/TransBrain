using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TransBrain.Application.Features.Geodata;

namespace TransBrain.Api.IntegrationTests;

public class GeodataEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object Override(string street, string city, double latitude = 48.37, double longitude = 10.90) => new
    {
        street,
        postalCode = "86150",
        city,
        country = "DE",
        latitude,
        longitude
    };

    private static async Task CreateCustomerWithLocationAsync(
        TransBrainApiFactory factory, string customerName, string street, string city)
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage customer = await admin.PostAsJsonAsync("/api/customers", new
        {
            name = customerName,
            street = "Hauptstraße 1",
            postalCode = "4020",
            city = "Linz",
            country = "AT",
            contactPerson = (string?)null,
            email = (string?)null,
            phone = (string?)null,
            vatId = (string?)null,
            paymentTermDays = 30
        });
        using JsonDocument json = JsonDocument.Parse(await customer.Content.ReadAsStringAsync());
        Guid customerId = json.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage location = await admin.PostAsJsonAsync($"/api/customers/{customerId}/locations", new
        {
            name = "Lager",
            street,
            postalCode = "86150",
            city,
            country = "DE",
            contacts = Array.Empty<object>()
        });
        location.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PutOverride_AsAdmin_ListsAddressAsManual()
    {
        await CreateCustomerWithLocationAsync(factory, "Geo Kunde GmbH", "Manuelleweg 1", "Augsburg");

        HttpResponseMessage put = await factory.CreateClientAs("admin")
            .PutAsJsonAsync("/api/geodata/overrides", Override("Manuelleweg 1", "Augsburg"));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/geodata");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        GeodataEntryResponse[]? entries = await list.Content.ReadFromJsonAsync<GeodataEntryResponse[]>();
        GeodataEntryResponse entry = entries!.Single(e => e.Street == "Manuelleweg 1");
        entry.Source.Should().Be(GeodataSources.Manual);
        entry.OverrideId.Should().NotBeNull();
        entry.Latitude.Should().Be(48.37);
        entry.LocationCount.Should().Be(1);
        entry.InUse.Should().BeTrue();
    }

    [Fact]
    public async Task PutOverride_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PutAsJsonAsync("/api/geodata/overrides", Override("Verbotenweg 1", "Augsburg"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutOverride_SameAddressTwice_KeepsSingleOverride()
    {
        HttpClient admin = factory.CreateClientAs("admin");

        HttpResponseMessage first = await admin
            .PutAsJsonAsync("/api/geodata/overrides", Override("Doppelweg 2", "Augsburg"));
        using JsonDocument firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Guid firstId = firstJson.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage second = await admin
            .PutAsJsonAsync("/api/geodata/overrides", Override("doppelweg 2", "AUGSBURG", 48.40, 10.95));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        secondJson.RootElement.GetProperty("id").GetGuid().Should().Be(firstId);
        secondJson.RootElement.GetProperty("latitude").GetDouble().Should().Be(48.40);
    }

    [Fact]
    public async Task PutOverride_InvalidLatitude_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PutAsJsonAsync("/api/geodata/overrides", Override("Kaputtweg 3", "Augsburg", latitude: 123));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteOverride_AsAdmin_AddressFallsBackToUnresolved()
    {
        await CreateCustomerWithLocationAsync(factory, "Rückfall GmbH", "Rückfallweg 4", "Augsburg");
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage put = await admin
            .PutAsJsonAsync("/api/geodata/overrides", Override("Rückfallweg 4", "Augsburg"));
        using JsonDocument json = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
        Guid id = json.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage delete = await admin.DeleteAsync($"/api/geodata/overrides/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        GeodataEntryResponse[]? entries = await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<GeodataEntryResponse[]>("/api/geodata");
        GeodataEntryResponse entry = entries!.Single(e => e.Street == "Rückfallweg 4");
        entry.Source.Should().Be(GeodataSources.Unresolved);
        entry.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOverride_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .DeleteAsync($"/api/geodata/overrides/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OverrideWithoutAnyUsage_IsListedAsNotInUse()
    {
        HttpResponseMessage put = await factory.CreateClientAs("admin")
            .PutAsJsonAsync("/api/geodata/overrides", Override("Verwaisterweg 5", "Augsburg"));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        GeodataEntryResponse[]? entries = await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<GeodataEntryResponse[]>("/api/geodata");
        GeodataEntryResponse entry = entries!.Single(e => e.Street == "Verwaisterweg 5");
        entry.InUse.Should().BeFalse();
        entry.Source.Should().Be(GeodataSources.Manual);
    }

    [Fact]
    public async Task Resolve_WithOverride_AnswersManualWithoutGeocoder()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        await admin.PutAsJsonAsync("/api/geodata/overrides", Override("Probeweg 6", "Augsburg"));

        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/geodata/resolve", new
        {
            street = "Probeweg 6",
            postalCode = "86150",
            city = "Augsburg",
            country = "DE"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("source").GetString().Should().Be(GeodataSources.Manual);
    }

    [Fact]
    public async Task Resolve_GeocoderOffline_ReturnsServiceUnavailable()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsJsonAsync("/api/geodata/resolve", new
            {
                street = "Unbekanntweg 7",
                postalCode = "86150",
                city = "Augsburg",
                country = "DE"
            });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
