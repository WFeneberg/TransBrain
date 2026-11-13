using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Customers.CustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;
using TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Customers;

public class CustomerLocationContactValidatorTests
{
    private static AddCustomerLocationCommand Add(params CustomerLocationContactData[] contacts) => new(
        Guid.CreateVersion7(), "Lager Süd", "Südbahnstraße 5", "1100", "Wien", "AT", Contacts: contacts);

    [Fact]
    public void Validate_AddWithoutContacts_IsValid()
    {
        ValidationResult result = new AddCustomerLocationCommandValidator().Validate(Add() with { Contacts = null });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AddWithSeveralValidContacts_IsValid()
    {
        ValidationResult result = new AddCustomerLocationCommandValidator().Validate(Add(
            new CustomerLocationContactData("Hans Huber", "+43 1 111", "h.huber@muster.at"),
            new CustomerLocationContactData("Eva Berger", Phone: "+43 1 222"),
            new CustomerLocationContactData("Ida Lang", Email: " ")));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AddContactWithoutName_IsKeyedByRowIndex()
    {
        ValidationResult result = new AddCustomerLocationCommandValidator().Validate(Add(
            new CustomerLocationContactData("Hans Huber"),
            new CustomerLocationContactData(" ", "+43 1 222")));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Contacts[1].Name");
    }

    [Fact]
    public void Validate_UpdateContactWithInvalidEmail_IsKeyedByRowIndex()
    {
        UpdateCustomerLocationCommand command = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Lager Süd", "Südbahnstraße 5", "1100", "Wien", "AT",
            Contacts: [new CustomerLocationContactData("Hans Huber", Email: "keine-mail")]);

        ValidationResult result = new UpdateCustomerLocationCommandValidator().Validate(command);

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Contacts[0].Email");
    }

    [Fact]
    public void Validate_AddTooManyContacts_ReturnsContactsError()
    {
        CustomerLocationContactData[] contacts = Enumerable.Range(1, CustomerLocation.MaxContacts + 1)
            .Select(i => new CustomerLocationContactData($"Kontakt {i}"))
            .ToArray();

        ValidationResult result = new AddCustomerLocationCommandValidator().Validate(Add(contacts));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Contacts");
    }
}
