using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Tours.CreateTour;

namespace TransBrain.Application.Tests.Features.Tours;

public class CreateTourCommandValidatorTests
{
    private readonly CreateTourCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyVehicleId_IsInvalid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.Empty, Guid.CreateVersion7()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTourCommand.VehicleId));
    }

    [Fact]
    public void Validate_EmptyDriverId_IsInvalid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.CreateVersion7(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTourCommand.DriverId));
    }

    [Fact]
    public void Validate_BothIdsPresent_IsValid()
    {
        ValidationResult result = _validator.Validate(
            new CreateTourCommand(new DateOnly(2027, 3, 1), Guid.CreateVersion7(), Guid.CreateVersion7()));

        result.IsValid.Should().BeTrue();
    }
}
