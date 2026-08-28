using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Value_SuccessfulResult_ReturnsValue()
    {
        Result<int> result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Value_FailedResult_ThrowsInvalidOperationException()
    {
        Result<int> result = Result<int>.Failure(Error.NotFound("X.NotFound", "missing"));

        result.IsSuccess.Should().BeFalse();
        FluentActions.Invoking(() => result.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        Result<string> result = Error.Conflict("X.Conflict", "clash");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("X.Conflict");
    }

    [Fact]
    public void Error_DefaultInstance_ThrowsInvalidOperationException()
    {
        Result<int> result = default;

        FluentActions.Invoking(() => result.Error).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Value_DefaultInstance_ThrowsInvalidOperationException()
    {
        Result<int> result = default;

        FluentActions.Invoking(() => result.Value).Should().Throw<InvalidOperationException>();
    }
}
