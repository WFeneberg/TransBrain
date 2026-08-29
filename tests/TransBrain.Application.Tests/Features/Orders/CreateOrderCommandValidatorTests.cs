using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CreateOrder;

namespace TransBrain.Application.Tests.Features.Orders;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    private static AddressPayload AnAddress(string name = "Absender GmbH") =>
        new(name, "Hauptstr. 1", "80331", "München", "DE");

    private static CreateOrderCommand ValidCommand => new(
        AnAddress("Absender GmbH"),
        AnAddress("Empfänger AG"),
        "Palettenware",
        12_000,
        8.4m,
        new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 16, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Validate_NullConsignor_IsInvalidAndDoesNotThrow()
    {
        Action act = () => _validator.Validate(ValidCommand with { Consignor = null! });

        act.Should().NotThrow();

        ValidationResult result = _validator.Validate(ValidCommand with { Consignor = null! });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullConsignee_IsInvalidAndDoesNotThrow()
    {
        Action act = () => _validator.Validate(ValidCommand with { Consignee = null! });

        act.Should().NotThrow();

        ValidationResult result = _validator.Validate(ValidCommand with { Consignee = null! });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_BlankConsignorName_FailsWithDottedPropertyName()
    {
        ValidationResult result = _validator.Validate(
            ValidCommand with { Consignor = AnAddress("   ") });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Consignor.Name");
    }
}
