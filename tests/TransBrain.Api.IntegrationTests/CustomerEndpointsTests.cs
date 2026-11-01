using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers;

namespace TransBrain.Api.IntegrationTests;

public class CustomerEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object NewCustomer(string name) => new
    {
        name,
        street = "Hauptstraße 1",
        postalCode = "4020",
        city = "Linz",
        country = "AT",
        contactPerson = "Anna Muster",
        email = "anna@muster.at",
        phone = (string?)null,
        vatId = "ATU12345678",
        paymentTermDays = 30
    };

    [Fact]
    public async Task PostCustomer_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/customers", NewCustomer("Anon GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCustomer_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/customers", NewCustomer("Dispo GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostCustomer_AsAdmin_ReturnsCreatedAndIsListable()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/customers", NewCustomer("Listbar GmbH"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage list = await factory.CreateClientAs("viewer")
            .GetAsync("/api/customers?pageSize=100");
        PagedResult<CustomerResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<CustomerResponse>>();
        page!.Items.Should().Contain(c => c.Name == "Listbar GmbH");
    }
}
