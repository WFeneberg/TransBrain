using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Features.Drivers.UpdateDriver;

namespace TransBrain.Application.Tests.Features.Drivers;

public class DriverSocialInsuranceDataValidatorTests
{
    private static CreateDriverCommand ValidCreate => new(
        "Frank", "Fahrer", ["CE"], new DateOnly(2028, 6, 30), null);

    [Fact]
    public void Validate_CreateWithoutSocialInsurance_IsValid()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithValidSocialInsurance_IsValid()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate with
        {
            SocialInsurance = new DriverSocialInsuranceData(
                DateOfBirth: new DateOnly(1980, 5, 17),
                PlaceOfBirth: "Linz",
                Nationality: "Österreich",
                SocialInsuranceNumber: "1234 170580",
                EntryDate: new DateOnly(2014, 10, 1),
                Iban: "AT611904300234573201",
                Bic: "RZOOAT2L")
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithInvalidSocialInsurance_ReportsEveryFieldWithDottedKey()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate with
        {
            SocialInsurance = new DriverSocialInsuranceData(
                DateOfBirth: new DateOnly(1899, 12, 31),
                EntryDate: new DateOnly(1899, 12, 31),
                PlaceOfBirth: new string('x', 201),
                Nationality: new string('x', 101),
                SocialInsuranceNumber: new string('1', 51),
                HealthInsurance: new string('x', 501),
                BankName: new string('x', 201),
                BankCode: new string('1', 21),
                AccountNumber: new string('1', 35),
                Bic: new string('X', 12),
                Iban: new string('X', 35),
                FamilyDoctor: new string('x', 501),
                FamilyDoctorPhone: new string('1', 51))
        });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "SocialInsurance.DateOfBirth",
            "SocialInsurance.EntryDate",
            "SocialInsurance.PlaceOfBirth",
            "SocialInsurance.Nationality",
            "SocialInsurance.SocialInsuranceNumber",
            "SocialInsurance.HealthInsurance",
            "SocialInsurance.BankName",
            "SocialInsurance.BankCode",
            "SocialInsurance.AccountNumber",
            "SocialInsurance.Bic",
            "SocialInsurance.Iban",
            "SocialInsurance.FamilyDoctor",
            "SocialInsurance.FamilyDoctorPhone");
    }

    [Fact]
    public void Validate_UpdateWithTooLongIban_ReportsField()
    {
        UpdateDriverCommand command = new(
            Guid.CreateVersion7(), "Frank", "Fahrer", ["CE"], new DateOnly(2028, 6, 30), null,
            new DriverSocialInsuranceData(Iban: new string('X', 35)));

        ValidationResult result = new UpdateDriverCommandValidator().Validate(command);

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("SocialInsurance.Iban");
    }
}
