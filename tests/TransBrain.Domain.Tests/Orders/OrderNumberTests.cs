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

    [Fact]
    public void From_NegativeSequence_Throws()
    {
        Action act = () => OrderNumber.From(2027, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void From_ZeroSequence_Throws()
    {
        Action act = () => OrderNumber.From(2027, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(999)]
    [InlineData(10_000)]
    public void From_YearOutsideFourDigits_Throws(int year)
    {
        Action act = () => OrderNumber.From(year, 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(2027, 1)]
    [InlineData(2027, 42)]
    [InlineData(1000, 1)]
    [InlineData(9999, 99_999)]
    [InlineData(2027, 123_456)]
    public void From_AnyValidInput_ProducesSomethingParseAccepts(int year, int sequence)
    {
        OrderNumber number = OrderNumber.From(year, sequence);

        Result<OrderNumber> parsed = OrderNumber.Parse(number.Value);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Value.Should().Be(number.Value);
    }
}
