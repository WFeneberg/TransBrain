using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Tests.Orders;

public class OrderNumberTests
{
    [Fact]
    public void From_YearAndSequence_FormatsWithFiveDigits()
    {
        OrderNumber number = OrderNumber.From(2027, 42);

        number.Value.Should().Be("TB-2027-00042");
    }

    [Fact]
    public void From_SequenceBeyondFiveDigits_DoesNotTruncate()
    {
        OrderNumber number = OrderNumber.From(2027, 123_456);

        number.Value.Should().Be("TB-2027-123456");
    }

    [Theory]
    [InlineData("TB-2027-00042")]
    [InlineData("TB-2027-123456")]
    public void Parse_WellFormedValue_RoundTrips(string value)
    {
        Result<OrderNumber> result = OrderNumber.Parse(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2027-00042")]
    [InlineData("TB-27-00042")]
    [InlineData("TB-2027-ABCDE")]
    public void Parse_MalformedValue_ReturnsValidationError(string? value)
    {
        Result<OrderNumber> result = OrderNumber.Parse(value);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("OrderNumber.Malformed");
    }
}
