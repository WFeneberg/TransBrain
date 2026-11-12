using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Invoices.CreateInvoice;

namespace TransBrain.Application.Tests.Features.Invoices;

public class CreateInvoiceCommandValidatorTests
{
    private readonly CreateInvoiceCommandValidator _validator = new();

    private static CreateInvoiceCommand ValidCommand => new(
        Guid.NewGuid(),
        19m,
        [new InvoiceLinePayload(Guid.NewGuid(), 450.50m)]);

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        ValidationResult result = _validator.Validate(ValidCommand);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyCustomerId_Fails()
    {
        ValidationResult result = _validator.Validate(ValidCommand with { CustomerId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Validate_VatRateOutOfRange_Fails(double rate)
    {
        ValidationResult result = _validator.Validate(ValidCommand with { VatRatePercent = (decimal)rate });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "VatRatePercent");
    }

    [Fact]
    public void Validate_NullLines_FailsWithoutThrowing()
    {
        ValidationResult result = _validator.Validate(ValidCommand with { Lines = null! });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyLines_Fails()
    {
        ValidationResult result = _validator.Validate(ValidCommand with { Lines = [] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuplicateOrder_Fails()
    {
        Guid orderId = Guid.NewGuid();

        ValidationResult result = _validator.Validate(ValidCommand with
        {
            Lines = [new InvoiceLinePayload(orderId, 100m), new InvoiceLinePayload(orderId, 200m)]
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("once per invoice"));
    }

    [Fact]
    public void Validate_LineWithEmptyOrderId_Fails()
    {
        ValidationResult result = _validator.Validate(ValidCommand with
        {
            Lines = [new InvoiceLinePayload(Guid.Empty, 100m)]
        });

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_LineWithNonPositiveAmount_Fails(double amount)
    {
        ValidationResult result = _validator.Validate(ValidCommand with
        {
            Lines = [new InvoiceLinePayload(Guid.NewGuid(), (decimal)amount)]
        });

        result.IsValid.Should().BeFalse();
    }
}
