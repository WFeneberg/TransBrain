using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.VehicleCatalog;

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

    [Fact]
    public async Task PutCustomer_WithAdditionalData_PersistsItAndCountsTheCustomerClassUsage()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/customers", NewCustomer("Zusatz GmbH"));
        CustomerResponse customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;
        customer.Additional.Should().Be(new CustomerAdditionalData());

        HttpResponseMessage updated = await admin.PutAsJsonAsync($"/api/customers/{customer.Id}", new
        {
            name = "Zusatz GmbH",
            street = "Hauptstraße 1",
            postalCode = "4020",
            city = "Linz",
            country = "AT",
            paymentTermDays = 30,
            additional = new
            {
                secondaryName = "Werk Vils",
                secondaryStreet = "Industriestraße 5",
                secondaryPostalCode = "6682",
                secondaryCity = "Vils",
                secondaryCountry = "at",
                foundedOn = "1985-04-01",
                workerCount = 120,
                salariedEmployeeCount = 35,
                requiresPalletCheck = true,
                excludeTollFromInvoice = true,
                reimburseToll = false,
                customerClass = "hutervilsall",
                comment = "Nur werktags anliefern."
            }
        });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        CustomerResponse reloaded = (await (await admin.GetAsync($"/api/customers/{customer.Id}"))
            .Content.ReadFromJsonAsync<CustomerResponse>())!;
        reloaded.Additional.Should().Be(new CustomerAdditionalData(
            "Werk Vils", "Industriestraße 5", "6682", "Vils", "AT", new DateOnly(1985, 4, 1),
            120, 35, true, true, false, "HuterVilsAll", "Nur werktags anliefern."));

        VehicleCatalogEntryResponse[] classes = (await (await admin.GetAsync("/api/vehicle-catalog/CustomerClass"))
            .Content.ReadFromJsonAsync<VehicleCatalogEntryResponse[]>())!;
        classes.Single(e => e.Code == "HuterVilsAll").UsageCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PostCustomer_NegativeWorkerCount_ReturnsValidationErrorKeyedByNestedField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/customers", new
        {
            name = "Klasse GmbH",
            street = "Hauptstraße 1",
            postalCode = "4020",
            city = "Linz",
            country = "AT",
            paymentTermDays = 30,
            additional = new { workerCount = -1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblem problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>())!;
        problem.Errors.Keys.Should().Contain("Additional.WorkerCount");
    }

    [Fact]
    public async Task PostCustomer_UnknownCustomerClass_ReturnsValidationErrorKeyedByField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/customers", new
        {
            name = "Klasse GmbH",
            street = "Hauptstraße 1",
            postalCode = "4020",
            city = "Linz",
            country = "AT",
            paymentTermDays = 30,
            additional = new { customerClass = "Unbekannt" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblem problem = (await response.Content.ReadFromJsonAsync<ValidationProblem>())!;
        problem.Errors.Keys.Should().Contain("Additional.CustomerClass");
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
