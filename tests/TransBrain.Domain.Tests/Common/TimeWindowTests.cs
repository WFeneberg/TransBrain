using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class TimeWindowTests
{
    private static readonly DateTimeOffset Morning = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Evening = new(2027, 3, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_FromBeforeTo_ReturnsWindowInUtc()
    {
        Result<TimeWindow> result = TimeWindow.Create(Morning, Evening);

        result.IsSuccess.Should().BeTrue();
        result.Value.From.Should().Be(Morning);
        result.Value.To.Should().Be(Evening);
        result.Value.From.Offset.Should().Be(TimeSpan.Zero);
        result.Value.To.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_NonUtcInput_NormalisesToUtcPreservingTheInstant()
    {
        DateTimeOffset berlinMorning = new(2027, 3, 1, 9, 0, 0, TimeSpan.FromHours(1));

        Result<TimeWindow> result = TimeWindow.Create(berlinMorning, Evening);

        result.Value.From.Offset.Should().Be(TimeSpan.Zero);
        result.Value.From.Should().Be(Morning);
    }

    [Fact]
    public void Create_FromEqualToTo_ReturnsValidationError()
    {
        Result<TimeWindow> result = TimeWindow.Create(Morning, Morning);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TimeWindow.FromNotBeforeTo");
    }

    [Fact]
    public void Create_FromAfterTo_ReturnsValidationError()
    {
        Result<TimeWindow> result = TimeWindow.Create(Evening, Morning);

        result.Error!.Code.Should().Be("TimeWindow.FromNotBeforeTo");
    }
}
