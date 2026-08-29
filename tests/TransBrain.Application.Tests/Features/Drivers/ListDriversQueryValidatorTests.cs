using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Drivers.ListDrivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class ListDriversQueryValidatorTests
{
    private readonly ListDriversQueryValidator _validator = new();

    [Fact]
    public void Validate_PageAtTheCap_IsValid()
    {
        ValidationResult result = _validator.Validate(new ListDriversQuery(Page: 10_000));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageBeyondTheCap_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListDriversQuery(Page: 10_001));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListDriversQuery.Page));
    }

    [Fact]
    public void Validate_PageZero_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListDriversQuery(Page: 0));

        result.IsValid.Should().BeFalse();
    }
}
