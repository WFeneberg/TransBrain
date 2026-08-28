using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class LicensePlateTests
{
    [Fact]
    public void Create_ValidPlate_ReturnsNormalizedUppercaseValue()
    {
        Result<LicensePlate> result = LicensePlate.Create("  m-ab 1234 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("M-AB 1234");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyPlate_ReturnsValidationError(string? input)
    {
        Result<LicensePlate> result = LicensePlate.Create(input);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_PlateLongerThan15Characters_ReturnsValidationError()
    {
        Result<LicensePlate> result = LicensePlate.Create(new string('A', 16));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.TooLong");
    }

    [Fact]
    public void Equals_SamePlateDifferentCasing_ReturnsTrue()
    {
        LicensePlate first = LicensePlate.Create("m-ab 1234").Value;
        LicensePlate second = LicensePlate.Create("M-AB 1234").Value;

        first.Should().Be(second);
    }
}
