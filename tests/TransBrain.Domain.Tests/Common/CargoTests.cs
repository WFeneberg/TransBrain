using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class CargoTests
{
    [Fact]
    public void Create_ValidArguments_ReturnsCargoWithTrimmedDescription()
    {
        Result<Cargo> result = Cargo.Create("  Palettenware ", 12_000, 8.4m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Palettenware");
        result.Value.WeightKg.Should().Be(12_000);
        result.Value.LoadMeters.Should().Be(8.4m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_BlankDescription_ReturnsValidationError(string? description)
    {
        Result<Cargo> result = Cargo.Create(description, 12_000, 8.4m);

        result.Error!.Code.Should().Be("Cargo.DescriptionRequired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveWeight_ReturnsValidationError(int weightKg)
    {
        Result<Cargo> result = Cargo.Create("Palettenware", weightKg, 8.4m);

        result.Error!.Code.Should().Be("Cargo.WeightKgNotPositive");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void Create_NonPositiveLoadMeters_ReturnsValidationError(double loadMeters)
    {
        Result<Cargo> result = Cargo.Create("Palettenware", 12_000, (decimal)loadMeters);

        result.Error!.Code.Should().Be("Cargo.LoadMetersNotPositive");
    }
}
