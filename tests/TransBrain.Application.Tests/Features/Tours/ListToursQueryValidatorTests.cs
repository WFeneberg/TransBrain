using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Tours.ListTours;

namespace TransBrain.Application.Tests.Features.Tours;

public class ListToursQueryValidatorTests
{
    private readonly ListToursQueryValidator _validator = new();

    [Fact]
    public void Validate_PageAtTheCap_IsValid()
    {
        ValidationResult result = _validator.Validate(new ListToursQuery(Page: 10_000));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageBeyondTheCap_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListToursQuery(Page: 10_001));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListToursQuery.Page));
    }

    [Fact]
    public void Validate_PageZero_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListToursQuery(Page: 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PageSizeBeyondTheCap_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListToursQuery(PageSize: 101));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListToursQuery.PageSize));
    }
}
