using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Domain.Tests.Customers;

public class CustomerLocationTests
{
    private static Address SampleAddress(string city = "Wien") =>
        Address.Create("Lager Süd", "Südbahnstraße 5", "1100", city, "AT").Value;

    private static CustomerLocationContact Contact(string name, string? phone = null, string? email = null) =>
        CustomerLocationContact.Create(name, phone, email).Value;

    [Fact]
    public void Create_ValidArguments_ReturnsLocationWithTrimmedName()
    {
        Guid customerId = Guid.CreateVersion7();

        Result<CustomerLocation> result = CustomerLocation.Create(customerId, "  Lager Süd ", SampleAddress(), []);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Name.Should().Be("Lager Süd");
        result.Value.Contacts.Should().BeEmpty();
    }

    [Fact]
    public void Create_SeveralContacts_KeepsEntryOrderAsSequence()
    {
        Result<CustomerLocation> result = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(),
            [Contact("Hans Huber", "+43 1 111"), Contact("Eva Berger", email: "e.berger@muster.at")]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Select(c => (c.Sequence, c.Name)).Should().Equal((1, "Hans Huber"), (2, "Eva Berger"));
        result.Value.Contacts[1].Email.Should().Be("e.berger@muster.at");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsValidationError(string name)
    {
        Result<CustomerLocation> result = CustomerLocation.Create(Guid.CreateVersion7(), name, SampleAddress(), []);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NameRequired");
    }

    [Fact]
    public void Create_TooManyContacts_ReturnsValidationError()
    {
        CustomerLocationContact[] contacts = Enumerable.Range(1, CustomerLocation.MaxContacts + 1)
            .Select(i => Contact($"Kontakt {i}"))
            .ToArray();

        Result<CustomerLocation> result = CustomerLocation.Create(Guid.CreateVersion7(), "Lager Süd", SampleAddress(), contacts);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.TooManyContacts");
    }

    [Fact]
    public void Create_OwnCompanyNameAndVatId_NormalizesBothTrimmed()
    {
        Result<CustomerLocation> result = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), [],
            companyName: " Lager Süd Logistik GmbH ", vatId: " ATU99999999 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Lager Süd Logistik GmbH");
        result.Value.VatId.Should().Be("ATU99999999");
    }

    [Fact]
    public void Create_BlankCompanyNameAndVatId_NormalizesToNull()
    {
        Result<CustomerLocation> result = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), [], companyName: "  ", vatId: "");

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().BeNull();
        result.Value.VatId.Should().BeNull();
    }

    [Fact]
    public void Update_OmittingCompanyNameAndVatId_ClearsThem()
    {
        CustomerLocation location = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), [],
            companyName: "Lager Süd Logistik GmbH", vatId: "ATU99999999").Value;

        Result<CustomerLocation> result = location.Update("Lager Süd", SampleAddress(), []);

        result.IsSuccess.Should().BeTrue();
        location.CompanyName.Should().BeNull();
        location.VatId.Should().BeNull();
    }

    [Fact]
    public void Update_ValidArguments_ReplacesContactsAndAddress()
    {
        CustomerLocation location = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), [Contact("Alt"), Contact("Auch alt")]).Value;

        Result<CustomerLocation> result = location.Update(
            "Werk Nord", SampleAddress("Graz"), [Contact("Neu", "+43 316 1", "neu@muster.at")]);

        result.IsSuccess.Should().BeTrue();
        location.Name.Should().Be("Werk Nord");
        location.Address.City.Should().Be("Graz");
        location.Contacts.Should().ContainSingle();
        location.Contacts[0].Should().BeEquivalentTo(new { Sequence = 1, Name = "Neu", Phone = "+43 316 1", Email = "neu@muster.at" });
    }

    [Fact]
    public void Update_BlankName_KeepsPreviousContacts()
    {
        CustomerLocation location = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), [Contact("Alt")]).Value;

        Result<CustomerLocation> result = location.Update(" ", SampleAddress(), [Contact("Neu")]);

        result.IsSuccess.Should().BeFalse();
        location.Contacts.Should().ContainSingle().Which.Name.Should().Be("Alt");
    }
}

public class CustomerLocationContactTests
{
    [Fact]
    public void Create_ValidArguments_TrimsAndNormalizesBlankValues()
    {
        Result<CustomerLocationContact> result = CustomerLocationContact.Create(" Hans Huber ", "   ", " h.huber@muster.at ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Hans Huber");
        result.Value.Phone.Should().BeNull();
        result.Value.Email.Should().Be("h.huber@muster.at");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_BlankName_ReturnsValidationError(string name)
    {
        Result<CustomerLocationContact> result = CustomerLocationContact.Create(name, "+43 1 111", null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocationContact.NameRequired");
    }
}
