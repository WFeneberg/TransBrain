using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using TransBrain.Application.Features.InvoiceDesigns;

namespace TransBrain.Api.IntegrationTests;

public class InvoiceDesignEndpointsTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    /// <summary>1x1 transparent PNG; small, but a genuine image.</summary>
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private static object ValidPayload(string companyName = "Nordlicht Transporte GmbH") => new
    {
        companyName,
        street = "Hafenstraße 9",
        postalCode = "20457",
        city = "Hamburg",
        countryCode = "DE",
        vatId = "DE222222222",
        taxNumber = "22/222/22222",
        email = "rechnung@nordlicht.example",
        phone = "+49 40 555 123",
        iban = "DE89370400440532013000",
        bic = "COBADEFFXXX",
        bankName = "Commerzbank",
        accentColor = "#0B5394",
        fontSizePoints = 11,
        logoPosition = "Left",
        logoBase64 = TinyPngBase64,
        logoContentType = "image/png",
        headerText = "Wir erlauben uns, folgende Leistungen zu berechnen:",
        footerText = "Vielen Dank für Ihren Auftrag!"
    };

    // Every test gets its own tenant so the per-tenant design row cannot leak between tests,
    // no matter in which order xUnit runs them.
    private HttpClient TenantClient(params string[] roles) =>
        factory.CreateClientForTenant(Guid.NewGuid(), roles);

    [Fact]
    public async Task GetInvoiceDesign_NothingSaved_ReturnsConfiguredDefaults()
    {
        HttpClient client = TenantClient("fahrer");

        HttpResponseMessage response = await client.GetAsync("/api/invoice-design");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        InvoiceDesignResponse design = (await response.Content.ReadFromJsonAsync<InvoiceDesignResponse>())!;
        design.IsCustomized.Should().BeFalse();
        design.CompanyName.Should().Be("TransBrain Logistik GmbH");
        design.Iban.Should().Be("DE02120300000000202051");
        design.AccentColor.Should().Be("#1F3864");
        design.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateInvoiceDesign_AsAdmin_PersistsForTenant()
    {
        Guid tenant = Guid.NewGuid();
        HttpClient admin = factory.CreateClientForTenant(tenant, "admin");

        HttpResponseMessage putResponse = await admin.PutAsJsonAsync("/api/invoice-design", ValidPayload());

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK, await putResponse.Content.ReadAsStringAsync());
        InvoiceDesignResponse saved = (await putResponse.Content.ReadFromJsonAsync<InvoiceDesignResponse>())!;
        saved.IsCustomized.Should().BeTrue();
        saved.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        saved.LogoBase64.Should().Be(TinyPngBase64);
        saved.UpdatedAt.Should().NotBeNull();

        HttpResponseMessage getResponse = await factory.CreateClientForTenant(tenant, "viewer")
            .GetAsync("/api/invoice-design");
        InvoiceDesignResponse fetched = (await getResponse.Content.ReadFromJsonAsync<InvoiceDesignResponse>())!;
        fetched.IsCustomized.Should().BeTrue();
        fetched.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        fetched.AccentColor.Should().Be("#0B5394");
        fetched.FontSizePoints.Should().Be(11);
        fetched.LogoPosition.Should().Be("Left");
        fetched.HeaderText.Should().Be("Wir erlauben uns, folgende Leistungen zu berechnen:");
    }

    [Fact]
    public async Task UpdateInvoiceDesign_SecondSave_OverwritesFirst()
    {
        Guid tenant = Guid.NewGuid();
        HttpClient admin = factory.CreateClientForTenant(tenant, "admin");
        (await admin.PutAsJsonAsync("/api/invoice-design", ValidPayload())).EnsureSuccessStatusCode();

        HttpResponseMessage second = await admin.PutAsJsonAsync(
            "/api/invoice-design", ValidPayload(companyName: "Umfirmiert AG"));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        InvoiceDesignResponse design = (await second.Content.ReadFromJsonAsync<InvoiceDesignResponse>())!;
        design.CompanyName.Should().Be("Umfirmiert AG");

        InvoiceDesignResponse fetched = (await admin.GetFromJsonAsync<InvoiceDesignResponse>("/api/invoice-design"))!;
        fetched.CompanyName.Should().Be("Umfirmiert AG");
    }

    [Theory]
    [InlineData("disponent")]
    [InlineData("viewer")]
    [InlineData("fahrer")]
    public async Task UpdateInvoiceDesign_WithoutAdminRole_ReturnsForbidden(string role)
    {
        HttpResponseMessage response = await TenantClient(role)
            .PutAsJsonAsync("/api/invoice-design", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateInvoiceDesign_InvalidPayload_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await TenantClient("admin").PutAsJsonAsync("/api/invoice-design", new
        {
            companyName = "",
            street = "Hafenstraße 9",
            postalCode = "20457",
            city = "Hamburg",
            countryCode = "Deutschland",
            vatId = "DE222222222",
            email = "keine-mail",
            iban = "DE89370400440532013000",
            accentColor = "blau",
            fontSizePoints = 99,
            logoPosition = "Center"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("CompanyName");
        body.Should().Contain("AccentColor");
        body.Should().Contain("FontSizePoints");
        body.Should().Contain("LogoPosition");
    }

    [Fact]
    public async Task UpdateInvoiceDesign_IsolatedPerTenant()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        (await factory.CreateClientForTenant(tenantA, "admin")
            .PutAsJsonAsync("/api/invoice-design", ValidPayload(companyName: "Firma A GmbH")))
            .EnsureSuccessStatusCode();

        InvoiceDesignResponse tenantBView = (await factory.CreateClientForTenant(tenantB, "admin")
            .GetFromJsonAsync<InvoiceDesignResponse>("/api/invoice-design"))!;

        tenantBView.IsCustomized.Should().BeFalse();
        tenantBView.CompanyName.Should().Be("TransBrain Logistik GmbH");

        (await factory.CreateClientForTenant(tenantB, "admin")
            .PutAsJsonAsync("/api/invoice-design", ValidPayload(companyName: "Firma B GmbH")))
            .EnsureSuccessStatusCode();

        InvoiceDesignResponse tenantAView = (await factory.CreateClientForTenant(tenantA, "admin")
            .GetFromJsonAsync<InvoiceDesignResponse>("/api/invoice-design"))!;
        tenantAView.CompanyName.Should().Be("Firma A GmbH");
    }

    [Fact]
    public async Task PreviewInvoiceDesign_ValidPayload_ReturnsPdf()
    {
        HttpResponseMessage response = await TenantClient("admin")
            .PostAsJsonAsync("/api/invoice-design/preview", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task PreviewInvoiceDesign_WithoutAdminRole_ReturnsForbidden()
    {
        HttpResponseMessage response = await TenantClient("disponent")
            .PostAsJsonAsync("/api/invoice-design/preview", ValidPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetInvoiceDesign_Anonymous_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/invoice-design");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
