using AwesomeAssertions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Billing;

public class InvoiceNumberTests
{
    [Fact]
    public void From_YearAndSequence_FormatsWithFiveDigits()
    {
        InvoiceNumber number = InvoiceNumber.From(2027, 42);

        number.Value.Should().Be("RE-2027-00042");
    }

    [Fact]
    public void From_SequenceBeyondFiveDigits_DoesNotTruncate()
    {
        InvoiceNumber number = InvoiceNumber.From(2027, 123_456);

        number.Value.Should().Be("RE-2027-123456");
    }

    [Theory]
    [InlineData("RE-2027-00042")]
    [InlineData("RE-2027-123456")]
    public void Parse_WellFormedValue_RoundTrips(string value)
    {
        Result<InvoiceNumber> result = InvoiceNumber.Parse(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2027-00042")]
    [InlineData("RE-27-00042")]
    [InlineData("RE-2027-ABCDE")]
    [InlineData("TB-2027-00042")]
    public void Parse_MalformedValue_ReturnsValidationError(string? value)
    {
        Result<InvoiceNumber> result = InvoiceNumber.Parse(value);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("InvoiceNumber.Malformed");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(999, 1)]
    [InlineData(10_000, 1)]
    public void From_YearOutsideFourDigits_Throws(int year, int sequence)
    {
        Action act = () => InvoiceNumber.From(year, sequence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void From_NonPositiveSequence_Throws(int sequence)
    {
        Action act = () => InvoiceNumber.From(2027, sequence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void From_AnyValidInput_ProducesSomethingParseAccepts()
    {
        InvoiceNumber number = InvoiceNumber.From(2027, 7);

        Result<InvoiceNumber> parsed = InvoiceNumber.Parse(number.Value);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.Value.Should().Be(number.Value);
    }
}
