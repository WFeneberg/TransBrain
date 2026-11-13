using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class DriverAdditionalDataValidatorTests
{
    private static CreateDriverCommand ValidCreate => new(
        "Frank", "Fahrer", ["CE"], new DateOnly(2028, 6, 30), null);

    [Fact]
    public void Validate_CreateWithoutAdditionalData_IsValid()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithValidAdditionalData_IsValid()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate with
        {
            Documents = new DriverDocumentsData("B072RRE2I55", "BH Linz-Land", new DateOnly(2008, 3, 12)),
            ClothingSizes = new DriverClothingSizesData("52", "44", "10"),
            Relatives = [new DriverRelativeData("Huber", "Anna", "Ehefrau", new DateOnly(1982, 7, 4), "+43 1 1")],
            SpecialDocuments =
                [new DriverSpecialDocumentData("TuvAssessment", new DateOnly(2024, 5, 2), new DateOnly(2029, 5, 2))],
            PhoneNumbers = [new DriverPhoneNumberData("+43 664 123", "Mobil")],
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_CreateWithInvalidDocumentsAndSizes_ReportsDottedKeys()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate with
        {
            Documents = new DriverDocumentsData(
                new string('x', DriverDocumentsInfo.MaxLicenseNumberLength + 1),
                new string('x', DriverDocumentsInfo.MaxIssuingAuthorityLength + 1),
                new DateOnly(1899, 12, 31)),
            ClothingSizes = new DriverClothingSizesData(ShoeSize: new string('4', 51)),
        });

        result.IsValid.Should().BeFalse();
        string[] keys = result.Errors.Select(e => e.PropertyName).ToArray();
        keys.Should().Contain("Documents.LicenseNumber");
        keys.Should().Contain("Documents.IssuingAuthority");
        keys.Should().Contain("Documents.IssueDate");
        keys.Should().Contain("ClothingSizes.ShoeSize");
    }

    [Fact]
    public void Validate_UpdateWithInvalidRows_ReportsIndexedKeys()
    {
        var update = new UpdateDriverCommand(
            Guid.NewGuid(), "Frank", "Fahrer", ["CE"], new DateOnly(2028, 6, 30), null)
        {
            Relatives =
            [
                new DriverRelativeData("Huber"),
                new DriverRelativeData("", DateOfBirth: new DateOnly(1899, 1, 1)),
            ],
            SpecialDocuments = [new DriverSpecialDocumentData("", ValidUntil: new DateOnly(1899, 1, 1))],
            PhoneNumbers = [new DriverPhoneNumberData("", Type: new string('x', 51))],
        };

        ValidationResult result = new UpdateDriverCommandValidator().Validate(update);

        result.IsValid.Should().BeFalse();
        string[] keys = result.Errors.Select(e => e.PropertyName).ToArray();
        keys.Should().Contain("Relatives[1].LastName");
        keys.Should().Contain("Relatives[1].DateOfBirth");
        keys.Should().Contain("SpecialDocuments[0].Type");
        keys.Should().Contain("SpecialDocuments[0].ValidUntil");
        keys.Should().Contain("PhoneNumbers[0].Number");
        keys.Should().Contain("PhoneNumbers[0].Type");
        keys.Should().NotContain(key => key.StartsWith("Relatives[0]"));
    }

    [Fact]
    public void Validate_CreateWithTooManyPhoneNumbers_ReportsListKey()
    {
        ValidationResult result = new CreateDriverCommandValidator().Validate(ValidCreate with
        {
            PhoneNumbers = Enumerable.Range(1, Driver.MaxPhoneNumbers + 1)
                .Select(i => new DriverPhoneNumberData($"+43 {i}"))
                .ToArray(),
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain("PhoneNumbers");
    }
}
