using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Orders.ListOrders;

namespace TransBrain.Application.Tests.Features.Orders;

public class ListOrdersQueryValidatorTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly ListOrdersQueryValidator _validator = new();

    [Fact]
    public void Validate_PageAtTheCap_IsValid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(Page: 10_000));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageBeyondTheCap_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(Page: 10_001));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.Page));
    }

    [Fact]
    public void Validate_PageZero_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(Page: 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_PageSizeBeyondTheCap_IsInvalid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(PageSize: 101));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.PageSize));
    }

    [Fact]
    public void Validate_PickupToBeforePickupFrom_IsInvalid()
    {
        ValidationResult result = _validator.Validate(
            new ListOrdersQuery(PickupFrom: March1, PickupTo: March1.AddDays(-1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOrdersQuery.PickupTo));
    }

    [Fact]
    public void Validate_PickupToEqualToPickupFrom_IsValid()
    {
        ValidationResult result = _validator.Validate(
            new ListOrdersQuery(PickupFrom: March1, PickupTo: March1));

        result.IsValid.Should().BeTrue();
    }

    // The range rule is guarded by a When clause, so a half-open range must stay valid rather
    // than dereferencing the null bound.
    [Fact]
    public void Validate_OnlyPickupToGiven_IsValid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(PickupTo: March1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OnlyPickupFromGiven_IsValid()
    {
        ValidationResult result = _validator.Validate(new ListOrdersQuery(PickupFrom: March1));

        result.IsValid.Should().BeTrue();
    }
}
