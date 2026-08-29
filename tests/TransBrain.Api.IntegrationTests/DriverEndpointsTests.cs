using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;

namespace TransBrain.Api.IntegrationTests;

public class DriverEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static object NewDriver(string lastName) => new
    {
        firstName = "Frank",
        lastName,
        licenseClasses = new[] { "C", "CE" },
        licenseValidUntil = "2028-06-30",
        externalUserId = (string?)null
    };

    [Fact]
    public async Task PostDriver_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/drivers", NewDriver("Anon"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostDriver_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/drivers", NewDriver("Dispo"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostDriver_AsAdmin_ReturnsCreatedAndIsListable()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PostAsJsonAsync("/api/drivers", NewDriver("Createable"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage list = await factory.CreateClientAs("viewer").GetAsync("/api/drivers");
        PagedResult<DriverResponse>? page = await list.Content.ReadFromJsonAsync<PagedResult<DriverResponse>>();
        page!.Items.Should().Contain(d => d.LastName == "Createable");
    }

    [Fact]
    public async Task GetDriverById_UnknownId_ReturnsNotFound()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .GetAsync($"/api/drivers/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutDriver_AsAdmin_UpdatesAndReturnsNewValues()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Updatable"));
        DriverResponse? driver = await created.Content.ReadFromJsonAsync<DriverResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/drivers/{driver!.Id}", new
        {
            firstName = "Franz",
            lastName = "Updatable",
            licenseClasses = new[] { "B" },
            licenseValidUntil = "2030-01-01",
            externalUserId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        DriverResponse? updated = await response.Content.ReadFromJsonAsync<DriverResponse>();
        updated!.FirstName.Should().Be("Franz");
        updated.LicenseClasses.Should().BeEquivalentTo(["B"]);
    }

    [Fact]
    public async Task DeleteDriver_AsAdmin_RemovesIt()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Deletable"));
        DriverResponse? driver = await created.Content.ReadFromJsonAsync<DriverResponse>();

        HttpResponseMessage response = await admin.DeleteAsync($"/api/drivers/{driver!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage after = await admin.GetAsync($"/api/drivers/{driver.Id}");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostDriver_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "",
            lastName = "",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("FirstName").And.Contain("LastName");
    }
}
