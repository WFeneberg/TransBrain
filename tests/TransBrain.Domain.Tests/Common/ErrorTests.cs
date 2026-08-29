using AwesomeAssertions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Common;

public class ErrorTests
{
    [Fact]
    public void Validation_SingleCodeAndMessage_LeavesFailuresNull()
    {
        Error error = Error.Validation("Vehicle.PayloadKgNotPositive", "Payload must be greater than zero.");

        error.Type.Should().Be(ErrorType.Validation);
        error.Failures.Should().BeNull();
    }

    [Fact]
    public void ValidationFailures_WithFieldErrors_ExposesThemKeyedByFieldName()
    {
        Dictionary<string, string[]> failures = new()
        {
            ["FirstName"] = ["'First Name' must not be empty."],
            ["LicenseValidUntil"] = ["'License Valid Until' must not be empty."]
        };

        Error error = Error.ValidationFailures(failures);

        error.Type.Should().Be(ErrorType.Validation);
        error.Failures.Should().NotBeNull();
        error.Failures!.Should().ContainKey("FirstName");
        error.Failures.Should().ContainKey("LicenseValidUntil");
    }

    [Fact]
    public void NotFound_Always_LeavesFailuresNull()
    {
        Error error = Error.NotFound("Driver.NotFound", "No driver with that id.");

        error.Failures.Should().BeNull();
    }
}
