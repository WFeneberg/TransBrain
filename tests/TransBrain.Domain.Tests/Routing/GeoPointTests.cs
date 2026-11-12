using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Domain.Tests.Routing;

public class GeoPointTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Create_OutOfRangeCoordinate_ReturnsValidationError(double latitude, double longitude)
    {
        Result<GeoPoint> result = GeoPoint.Create(latitude, longitude);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void StraightLineMetersTo_MunichToBerlin_IsAboutFiveHundredKilometers()
    {
        GeoPoint munich = GeoPoint.Create(48.137, 11.575).Value;
        GeoPoint berlin = GeoPoint.Create(52.520, 13.405).Value;

        double meters = munich.StraightLineMetersTo(berlin);

        meters.Should().BeInRange(495_000, 515_000);
        berlin.StraightLineMetersTo(munich).Should().Be(meters);
    }

    [Fact]
    public void StraightLineMetersTo_SamePoint_IsZero()
    {
        GeoPoint point = GeoPoint.Create(48.137, 11.575).Value;

        point.StraightLineMetersTo(point).Should().Be(0);
    }
}
