using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers.CustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;

namespace TransBrain.Api.IntegrationTests;

public class CustomerLocationEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object NewLocation(string name) => new
    {
        name,
        street = "Südbahnstraße 5",
        postalCode = "1100",
        city = "Wien",
        country = "AT",
        contacts = new object[]
        {
            new { name = "Hans Huber", email = "h.huber@muster.at" },
            new { name = "Eva Berger", phone = "+43 1 222" }
        }
    };

    private static async Task<Guid> CreateCustomerAsync(TransBrainApiFactory factory, string name)
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/customers", new
        {
            name,
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
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task PostLocation_AsDisponent_ReturnsForbidden()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Dispo Kunde GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync($"/api/customers/{customerId}/locations", NewLocation("Lager"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostLocation_AsAdmin_IsListedUnderItsCustomer()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Mehrere Orte GmbH");

        HttpResponseMessage create = await factory.CreateClientAs("admin")
            .PostAsJsonAsync($"/api/customers/{customerId}/locations", NewLocation("Lager Wien"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage list = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/customers/{customerId}/locations");
        CustomerLocationResponse[]? locations = await list.Content.ReadFromJsonAsync<CustomerLocationResponse[]>();
        CustomerLocationResponse location = locations!.Should().ContainSingle().Subject;
        location.Contacts.Should().Equal(
            new CustomerLocationContactData("Hans Huber", Email: "h.huber@muster.at"),
            new CustomerLocationContactData("Eva Berger", "+43 1 222"));
    }

    [Fact]
    public async Task PostLocation_UnknownCustomer_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync($"/api/customers/{Guid.CreateVersion7()}/locations", NewLocation("Lager"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListAllLocations_IncludesLocationWithCustomerName()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Übersicht GmbH");
        await factory.CreateClientAs("admin").PostAsJsonAsync($"/api/customers/{customerId}/locations", NewLocation("Depot Graz"));

        HttpResponseMessage response = await factory.CreateClientAs("viewer").GetAsync("/api/customer-locations?pageSize=100");
        PagedResult<CustomerLocationListItem>? page = await response.Content.ReadFromJsonAsync<PagedResult<CustomerLocationListItem>>();

        page!.Items.Should().ContainSingle(i => i.Name == "Depot Graz" && i.CustomerName == "Übersicht GmbH")
            .Which.Contacts.Select(c => c.Name).Should().Equal("Hans Huber", "Eva Berger");
    }

    [Fact]
    public async Task GetPosition_WithoutGeocoderConfigured_ReturnsServiceUnavailable()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Karten GmbH");
        HttpResponseMessage create = await factory.CreateClientAs("admin")
            .PostAsJsonAsync($"/api/customers/{customerId}/locations", NewLocation("Depot Karte"));
        using JsonDocument json = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        Guid locationId = json.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/customers/{customerId}/locations/{locationId}/position");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PutLocation_NewContacts_ReplacesTheWholeList()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Kontaktwechsel GmbH");
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage create = await admin.PostAsJsonAsync($"/api/customers/{customerId}/locations", NewLocation("Lager Linz"));
        CustomerLocationResponse created = (await create.Content.ReadFromJsonAsync<CustomerLocationResponse>())!;

        HttpResponseMessage update = await admin.PutAsJsonAsync($"/api/customers/{customerId}/locations/{created.Id}", new
        {
            name = "Lager Linz",
            street = "Südbahnstraße 5",
            postalCode = "1100",
            city = "Wien",
            country = "AT",
            contacts = new object[]
            {
                new { name = "Eva Berger", phone = "+43 1 999", email = "e.berger@muster.at" },
                new { name = "Ida Lang" },
                new { name = "Max Moser", phone = "+43 1 777" }
            }
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        CustomerLocationResponse[] locations = (await admin.GetFromJsonAsync<CustomerLocationResponse[]>($"/api/customers/{customerId}/locations"))!;
        locations.Should().ContainSingle().Which.Contacts.Should().Equal(
            new CustomerLocationContactData("Eva Berger", "+43 1 999", "e.berger@muster.at"),
            new CustomerLocationContactData("Ida Lang"),
            new CustomerLocationContactData("Max Moser", "+43 1 777"));
    }

    [Fact]
    public async Task PostLocation_ContactWithoutName_ReturnsBadRequestKeyedByRow()
    {
        Guid customerId = await CreateCustomerAsync(factory, "Namenlos GmbH");

        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync($"/api/customers/{customerId}/locations", new
        {
            name = "Lager",
            street = "Südbahnstraße 5",
            postalCode = "1100",
            city = "Wien",
            country = "AT",
            contacts = new object[] { new { name = "Hans Huber" }, new { name = "", email = "keine-mail" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblem problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>())!;
        problem.Errors.Keys.Should().Contain(["Contacts[1].Name", "Contacts[1].Email"]);
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
