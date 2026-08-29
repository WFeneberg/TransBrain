using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class AddressTests
{
    [Fact]
    public void Create_ValidArguments_TrimsEveryFieldAndUppercasesCountry()
    {
        Result<Address> result = Address.Create("  Meier GmbH ", " Hauptstr. 1 ", " 80331 ", " München ", " de ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Meier GmbH");
        result.Value.Street.Should().Be("Hauptstr. 1");
        result.Value.PostalCode.Should().Be("80331");
        result.Value.City.Should().Be("München");
        result.Value.Country.Should().Be("DE");
    }

    [Theory]
    [InlineData(null, "Address.NameRequired")]
    [InlineData("", "Address.NameRequired")]
    [InlineData("   ", "Address.NameRequired")]
    public void Create_BlankName_ReturnsValidationError(string? name, string expectedCode)
    {
        Result<Address> result = Address.Create(name, "Hauptstr. 1", "80331", "München", "DE");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_BlankStreet_ReturnsValidationError()
    {
        Result<Address> result = Address.Create("Meier GmbH", "  ", "80331", "München", "DE");

        result.Error!.Code.Should().Be("Address.StreetRequired");
    }

    [Fact]
    public void Create_BlankPostalCode_ReturnsValidationError()
    {
        Result<Address> result = Address.Create("Meier GmbH", "Hauptstr. 1", "", "München", "DE");

        result.Error!.Code.Should().Be("Address.PostalCodeRequired");
    }

    [Fact]
    public void Create_BlankCity_ReturnsValidationError()
    {
        Result<Address> result = Address.Create("Meier GmbH", "Hauptstr. 1", "80331", " ", "DE");

        result.Error!.Code.Should().Be("Address.CityRequired");
    }

    [Theory]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("")]
    [InlineData("1A")]
    public void Create_CountryThatIsNotTwoLetters_ReturnsValidationError(string country)
    {
        Result<Address> result = Address.Create("Meier GmbH", "Hauptstr. 1", "80331", "München", country);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.CountryInvalid");
    }

    [Fact]
    public void Equals_SameValuesDifferentCountryCasing_ReturnsTrue()
    {
        Address first = Address.Create("Meier GmbH", "Hauptstr. 1", "80331", "München", "de").Value;
        Address second = Address.Create("Meier GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        first.Should().Be(second);
    }
}
