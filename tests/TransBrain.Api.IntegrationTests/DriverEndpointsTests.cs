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
    public async Task GetDrivers_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/drivers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutDriver_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PutAsJsonAsync($"/api/drivers/{Guid.CreateVersion7()}", new
            {
                firstName = "Franz",
                lastName = "Forbidden",
                licenseClasses = new[] { "B" },
                licenseValidUntil = "2030-01-01",
                externalUserId = (string?)null
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteDriver_AsDisponent_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .DeleteAsync($"/api/drivers/{Guid.CreateVersion7()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact]
    public async Task PostDriver_DuplicateExternalUserId_ReturnsConflict()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        object firstDriver = new
        {
            firstName = "Frank",
            lastName = "First",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = "keycloak-sub-duplicate"
        };
        object secondDriver = new
        {
            firstName = "Frank",
            lastName = "Second",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = "keycloak-sub-duplicate"
        };

        await admin.PostAsJsonAsync("/api/drivers", firstDriver);
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/drivers", secondDriver);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Two drivers with no login (externalUserId == null) must not collide with each other: the
    // unique index on ExternalUserId is filtered to non-NULL values for exactly this reason.
    [Fact]
    public async Task PostDriver_TwoDriversWithNoExternalUserId_BothSucceed()
    {
        HttpClient admin = factory.CreateClientAs("admin");

        HttpResponseMessage first = await admin.PostAsJsonAsync("/api/drivers", NewDriver("NoLoginOne"));
        HttpResponseMessage second = await admin.PostAsJsonAsync("/api/drivers", NewDriver("NoLoginTwo"));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PutDriver_WithSocialInsuranceData_PersistsIt()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Sozialdaten"));
        DriverResponse driver = (await created.Content.ReadFromJsonAsync<DriverResponse>())!;
        driver.SocialInsurance.Should().Be(new DriverSocialInsuranceData());

        HttpResponseMessage updated = await admin.PutAsJsonAsync($"/api/drivers/{driver.Id}", new
        {
            firstName = "Frank",
            lastName = "Sozialdaten",
            licenseClasses = new[] { "C", "CE" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            socialInsurance = new
            {
                dateOfBirth = "1980-05-17",
                placeOfBirth = "Linz",
                nationality = "Österreich",
                socialInsuranceNumber = "1234 170580",
                entryDate = "2014-10-01",
                healthInsurance = "ÖGK Oberösterreich",
                bankName = "Raiffeisenbank",
                bankCode = "34000",
                accountNumber = "00012345678",
                bic = "rzooat2l",
                iban = "at611904300234573201",
                familyDoctor = "Dr. Maria Huber",
                familyDoctorPhone = "+43 732 123456"
            }
        });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        DriverResponse reloaded = (await (await admin.GetAsync($"/api/drivers/{driver.Id}"))
            .Content.ReadFromJsonAsync<DriverResponse>())!;
        reloaded.SocialInsurance.Should().Be(new DriverSocialInsuranceData(
            new DateOnly(1980, 5, 17), "Linz", "Österreich", "1234 170580", new DateOnly(2014, 10, 1),
            "ÖGK Oberösterreich", "Raiffeisenbank", "34000", "00012345678", "RZOOAT2L",
            "AT611904300234573201", "Dr. Maria Huber", "+43 732 123456"));
    }

    [Fact]
    public async Task PostDriver_WithSocialInsuranceData_ReturnsItInTheCreatedDriver()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "SozialdatenNeu",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            socialInsurance = new { dateOfBirth = "1980-05-17", placeOfBirth = "Linz" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        DriverResponse driver = (await response.Content.ReadFromJsonAsync<DriverResponse>())!;
        driver.SocialInsurance.DateOfBirth.Should().Be(new DateOnly(1980, 5, 17));
        driver.SocialInsurance.PlaceOfBirth.Should().Be("Linz");
    }

    [Fact]
    public async Task PostDriver_TooEarlyDateOfBirth_ReturnsValidationErrorKeyedByNestedField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "ZuFrüh",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            socialInsurance = new { dateOfBirth = "1899-12-31" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SocialInsurance.DateOfBirth");
    }

    [Fact]
    public async Task PutDriver_WithAdditionalData_PersistsDocumentsSizesAndLists()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("Zusatzdaten"));
        DriverResponse driver = (await created.Content.ReadFromJsonAsync<DriverResponse>())!;
        driver.Documents.Should().Be(new DriverDocumentsData());
        driver.ClothingSizes.Should().Be(new DriverClothingSizesData());
        driver.Relatives.Should().BeEmpty();

        HttpResponseMessage updated = await admin.PutAsJsonAsync($"/api/drivers/{driver.Id}", new
        {
            firstName = "Frank",
            lastName = "Zusatzdaten",
            licenseClasses = new[] { "C", "CE" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            documents = new
            {
                licenseNumber = "B072RRE2I55",
                issuingAuthority = "BH Linz-Land",
                issueDate = "2008-03-12"
            },
            clothingSizes = new { clothingSize = "52", shoeSize = "44", gloveSize = "10" },
            relatives = new[]
            {
                new { lastName = "Huber", firstName = "Anna", relationship = "Ehefrau",
                    dateOfBirth = "1982-07-04", phone = (string?)"+43 664 1234567" },
                new { lastName = "Huber", firstName = "Karl", relationship = "Sohn",
                    dateOfBirth = "2010-01-15", phone = (string?)null },
            },
            specialDocuments = new[]
            {
                new { type = "tuvAssessment", lastInspectionDate = "2024-05-02",
                    validUntil = "2029-05-02", comment = "Basiskurs + Tank" },
            },
            phoneNumbers = new[]
            {
                new { number = "+43 732 111111", type = "private", comment = (string?)null },
                new { number = "+43 664 9876543", type = "CarPhone", comment = (string?)"dienstlich" },
            }
        });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        DriverResponse reloaded = (await (await admin.GetAsync($"/api/drivers/{driver.Id}"))
            .Content.ReadFromJsonAsync<DriverResponse>())!;
        reloaded.Documents.Should().Be(
            new DriverDocumentsData("B072RRE2I55", "BH Linz-Land", new DateOnly(2008, 3, 12)));
        reloaded.ClothingSizes.Should().Be(new DriverClothingSizesData("52", "44", "10"));
        reloaded.Relatives.Should().Equal(
            new DriverRelativeData("Huber", "Anna", "Ehefrau", new DateOnly(1982, 7, 4), "+43 664 1234567"),
            new DriverRelativeData("Huber", "Karl", "Sohn", new DateOnly(2010, 1, 15)));
        reloaded.SpecialDocuments.Should().Equal(
            new DriverSpecialDocumentData(
                "TuvAssessment", new DateOnly(2024, 5, 2), new DateOnly(2029, 5, 2), "Basiskurs + Tank"));
        reloaded.PhoneNumbers.Should().Equal(
            new DriverPhoneNumberData("+43 732 111111", "Private"),
            new DriverPhoneNumberData("+43 664 9876543", "CarPhone", "dienstlich"));
    }

    [Fact]
    public async Task PutDriver_WithoutAdditionalData_ClearsPreviouslyStoredLists()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "ZusatzWeg",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            phoneNumbers = new[] { new { number = "+43 1 555", type = "Private" } }
        });
        DriverResponse driver = (await created.Content.ReadFromJsonAsync<DriverResponse>())!;
        driver.PhoneNumbers.Should().ContainSingle();

        HttpResponseMessage updated = await admin.PutAsJsonAsync(
            $"/api/drivers/{driver.Id}", NewDriver("ZusatzWeg"));

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        DriverResponse reloaded = (await (await admin.GetAsync($"/api/drivers/{driver.Id}"))
            .Content.ReadFromJsonAsync<DriverResponse>())!;
        reloaded.PhoneNumbers.Should().BeEmpty();
    }

    [Fact]
    public async Task PostDriver_UnknownPhoneType_ReturnsValidationErrorKeyedByIndexedField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "FalscherTyp",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            phoneNumbers = new[] { new { number = "+43 1 555", type = "Mobil" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PhoneNumbers[0].Type");
    }

    [Fact]
    public async Task PostDriver_UnknownSpecialDocumentType_ReturnsValidationErrorKeyedByIndexedField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "FalschesPapier",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            specialDocuments = new[] { new { type = "ADR-Schein" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SpecialDocuments[0].Type").And.Contain("driver document type");
    }

    [Fact]
    public async Task PostDriver_InvalidRelativeRow_ReturnsValidationErrorKeyedByIndexedField()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin").PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Frank",
            lastName = "KaputteZeile",
            licenseClasses = new[] { "C" },
            licenseValidUntil = "2028-06-30",
            externalUserId = (string?)null,
            relatives = new[]
            {
                new { lastName = "Huber", dateOfBirth = (string?)"1982-07-04" },
                new { lastName = "", dateOfBirth = (string?)"1899-01-01" },
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Relatives[1].LastName").And.Contain("Relatives[1].DateOfBirth");
    }

    [Fact]
    public async Task PutDriver_TwoInvalidFields_ReturnsBothKeyedByFieldName()
    {
        HttpClient admin = factory.CreateClientAs("admin");
        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/drivers", NewDriver("ToInvalidate"));
        DriverResponse? driver = await created.Content.ReadFromJsonAsync<DriverResponse>();

        HttpResponseMessage response = await admin.PutAsJsonAsync($"/api/drivers/{driver!.Id}", new
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
