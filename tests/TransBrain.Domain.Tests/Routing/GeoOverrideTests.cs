using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Domain.Tests.Routing;

public class GeoOverrideTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInput_NormalisesFieldsAndBuildsKey()
    {
        Result<GeoOverride> result = GeoOverride.Create(
            " Hauptstraße 1 ", " 86150 ", " Augsburg ", "de", 48.37, 10.90, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Street.Should().Be("Hauptstraße 1");
        result.Value.PostalCode.Should().Be("86150");
        result.Value.City.Should().Be("Augsburg");
        result.Value.Country.Should().Be("DE");
        result.Value.AddressKey.Should().Be("de|86150|augsburg|hauptstraße 1");
        result.Value.Position.Latitude.Should().Be(48.37);
        result.Value.Position.Longitude.Should().Be(10.90);
        result.Value.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null, "86150", "Augsburg", "DE")]
    [InlineData("Hauptstraße 1", "", "Augsburg", "DE")]
    [InlineData("Hauptstraße 1", "86150", " ", "DE")]
    [InlineData("Hauptstraße 1", "86150", "Augsburg", "Deutschland")]
    public void Create_InvalidAddress_ReturnsValidation(string? street, string postalCode, string city, string country)
    {
        Result<GeoOverride> result = GeoOverride.Create(street, postalCode, city, country, 48.37, 10.90, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData(91, 10.90)]
    [InlineData(-91, 10.90)]
    [InlineData(48.37, 181)]
    [InlineData(48.37, -181)]
    [InlineData(double.NaN, 10.90)]
    public void Create_CoordinateOutOfRange_ReturnsValidation(double latitude, double longitude)
    {
        Result<GeoOverride> result = GeoOverride.Create(
            "Hauptstraße 1", "86150", "Augsburg", "DE", latitude, longitude, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Reposition_ValidCoordinates_MovesPointAndStampsTime()
    {
        GeoOverride geoOverride = GeoOverride.Create(
            "Hauptstraße 1", "86150", "Augsburg", "DE", 48.37, 10.90, Now).Value;
        DateTimeOffset later = Now.AddHours(1);

        Result<Unit> result = geoOverride.Reposition(48.40, 10.95, later);

        result.IsSuccess.Should().BeTrue();
        geoOverride.Position.Latitude.Should().Be(48.40);
        geoOverride.Position.Longitude.Should().Be(10.95);
        geoOverride.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Reposition_InvalidCoordinates_KeepsExistingPosition()
    {
        GeoOverride geoOverride = GeoOverride.Create(
            "Hauptstraße 1", "86150", "Augsburg", "DE", 48.37, 10.90, Now).Value;

        Result<Unit> result = geoOverride.Reposition(123, 10.95, Now.AddHours(1));

        result.IsSuccess.Should().BeFalse();
        geoOverride.Position.Latitude.Should().Be(48.37);
        geoOverride.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void BuildKey_FromAddress_MatchesFieldOverload()
    {
        Address address = Address.Create("Lager", "Hauptstraße 1", "86150", "Augsburg", "de").Value;

        GeoOverride.BuildKey(address).Should().Be(
            GeoOverride.BuildKey("Hauptstraße 1", "86150", "Augsburg", "DE"));
    }

    [Fact]
    public void BuildKey_DiffersOnlyByCase_ProducesSameKey()
    {
        GeoOverride.BuildKey("HAUPTSTRASSE 1", "86150", "AUGSBURG", "DE").Should().Be(
            GeoOverride.BuildKey("hauptstrasse 1", "86150", "augsburg", "de"));
    }
}
