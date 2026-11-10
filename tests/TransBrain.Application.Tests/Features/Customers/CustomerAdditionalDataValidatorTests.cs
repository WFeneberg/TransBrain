using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Customers.CreateCustomer;
using TransBrain.Application.Features.Customers.UpdateCustomer;

namespace TransBrain.Application.Tests.Features.Customers;

public class CustomerAdditionalDataValidatorTests
{
    private static CreateCustomerCommand ValidCreate => new(
        "Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT", null, null, null, null, 30);

    [Fact]
    public void Validate_CreateWithoutAdditional_IsValid()
    {
        ValidationResult result = new CreateCustomerCommandValidator().Validate(ValidCreate);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithValidAdditional_IsValid()
    {
        ValidationResult result = new CreateCustomerCommandValidator().Validate(ValidCreate with
        {
            Additional = new CustomerAdditionalData(
                SecondaryCountry: "de", FoundedOn: new DateOnly(1990, 1, 1), WorkerCount: 5, CustomerClass: "A")
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithInvalidAdditional_ReportsEveryFieldWithDottedKey()
    {
        ValidationResult result = new CreateCustomerCommandValidator().Validate(ValidCreate with
        {
            Additional = new CustomerAdditionalData(
                SecondaryCountry: "Deutschland",
                FoundedOn: new DateOnly(1700, 1, 1),
                WorkerCount: -1,
                SalariedEmployeeCount: 2_000_000,
                CustomerClass: new string('A', 21),
                Comment: new string('x', 2001))
        });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "Additional.SecondaryCountry",
            "Additional.FoundedOn",
            "Additional.WorkerCount",
            "Additional.SalariedEmployeeCount",
            "Additional.CustomerClass",
            "Additional.Comment");
    }

    [Fact]
    public void Validate_UpdateWithTooLongSecondaryCity_ReportsField()
    {
        UpdateCustomerCommand command = new(
            Guid.CreateVersion7(), "Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT", null, null, null, null, 30,
            new CustomerAdditionalData(SecondaryCity: new string('x', 201)));

        ValidationResult result = new UpdateCustomerCommandValidator().Validate(command);

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Additional.SecondaryCity");
    }
}
