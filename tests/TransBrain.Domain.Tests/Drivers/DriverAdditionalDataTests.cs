using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Domain.Tests.Drivers;

public class DriverAdditionalDataTests
{
    private static readonly DateOnly ValidUntil = new(2028, 6, 30);
    private static readonly LicenseClass[] Classes = [LicenseClass.CE];

    [Fact]
    public void DocumentsCreate_AllValues_NormalizesText()
    {
        Result<DriverDocumentsInfo> result = DriverDocumentsInfo.Create(
            licenseNumber: " B072RRE2I55 ",
            issuingAuthority: "BH Linz-Land\nKärntner Straße 16, 4020 Linz",
            issueDate: new DateOnly(2008, 3, 12));

        result.IsSuccess.Should().BeTrue();
        result.Value.LicenseNumber.Should().Be("B072RRE2I55");
        result.Value.IssuingAuthority.Should().StartWith("BH Linz-Land");
        result.Value.IssueDate.Should().Be(new DateOnly(2008, 3, 12));
    }

    [Fact]
    public void DocumentsCreate_NoValues_EqualsEmpty()
    {
        DriverDocumentsInfo.Create().Value.Should().Be(DriverDocumentsInfo.Empty);
    }

    [Fact]
    public void DocumentsCreate_IssueDateBefore1900_Fails()
    {
        Result<DriverDocumentsInfo> result = DriverDocumentsInfo.Create(issueDate: new DateOnly(1899, 12, 31));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseIssueDateTooEarly");
    }

    [Fact]
    public void DocumentsCreate_LicenseNumberTooLong_Fails()
    {
        Result<DriverDocumentsInfo> result = DriverDocumentsInfo.Create(
            licenseNumber: new string('x', DriverDocumentsInfo.MaxLicenseNumberLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.DocumentsTextTooLong");
    }

    [Fact]
    public void ClothingSizesCreate_AllValues_NormalizesText()
    {
        Result<DriverClothingSizes> result = DriverClothingSizes.Create(" 52 ", "44", " 10 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.ClothingSize.Should().Be("52");
        result.Value.ShoeSize.Should().Be("44");
        result.Value.GloveSize.Should().Be("10");
    }

    [Fact]
    public void ClothingSizesCreate_TooLong_Fails()
    {
        Result<DriverClothingSizes> result = DriverClothingSizes.Create(
            shoeSize: new string('4', DriverClothingSizes.MaxSizeLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.ClothingSizesTooLong");
    }

    [Fact]
    public void RelativeCreate_AllValues_NormalizesText()
    {
        Result<DriverRelative> result = DriverRelative.Create(
            " Huber ", " Anna ", " Ehefrau ", new DateOnly(1982, 7, 4), " +43 664 1234567 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.LastName.Should().Be("Huber");
        result.Value.FirstName.Should().Be("Anna");
        result.Value.Relationship.Should().Be("Ehefrau");
        result.Value.DateOfBirth.Should().Be(new DateOnly(1982, 7, 4));
        result.Value.Phone.Should().Be("+43 664 1234567");
    }

    [Fact]
    public void RelativeCreate_EmptyLastName_Fails()
    {
        Result<DriverRelative> result = DriverRelative.Create("  ");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("DriverRelative.LastNameRequired");
    }

    [Fact]
    public void RelativeCreate_DateOfBirthBefore1900_Fails()
    {
        Result<DriverRelative> result = DriverRelative.Create("Huber", dateOfBirth: new DateOnly(1899, 1, 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("DriverRelative.DateOfBirthTooEarly");
    }

    [Fact]
    public void SpecialDocumentCreate_AllValues_NormalizesText()
    {
        Result<DriverSpecialDocument> result = DriverSpecialDocument.Create(
            " TuvAssessment ", new DateOnly(2024, 5, 2), new DateOnly(2029, 5, 2), " Basiskurs + Tank ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("TuvAssessment");
        result.Value.LastInspectionDate.Should().Be(new DateOnly(2024, 5, 2));
        result.Value.ValidUntil.Should().Be(new DateOnly(2029, 5, 2));
        result.Value.Comment.Should().Be("Basiskurs + Tank");
    }

    [Fact]
    public void SpecialDocumentCreate_EmptyType_Fails()
    {
        Result<DriverSpecialDocument> result = DriverSpecialDocument.Create(" ");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("DriverSpecialDocument.TypeRequired");
    }

    [Fact]
    public void SpecialDocumentCreate_ValidUntilBefore1900_Fails()
    {
        Result<DriverSpecialDocument> result = DriverSpecialDocument.Create(
            "ADR", validUntil: new DateOnly(1899, 6, 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("DriverSpecialDocument.DateTooEarly");
    }

    [Fact]
    public void PhoneNumberCreate_AllValues_NormalizesText()
    {
        Result<DriverPhoneNumber> result = DriverPhoneNumber.Create(
            " +43 664 9876543 ", " Mobil ", " dienstlich ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be("+43 664 9876543");
        result.Value.Type.Should().Be("Mobil");
        result.Value.Comment.Should().Be("dienstlich");
    }

    [Fact]
    public void PhoneNumberCreate_EmptyNumber_Fails()
    {
        Result<DriverPhoneNumber> result = DriverPhoneNumber.Create("");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("DriverPhoneNumber.NumberRequired");
    }

    [Fact]
    public void DriverCreate_WithCollections_AssignsSequencesInOrder()
    {
        Result<Driver> result = Driver.Create(
            "Max", "Fahrer", Classes, ValidUntil, null,
            relatives:
            [
                DriverRelative.Create("Huber", "Anna").Value,
                DriverRelative.Create("Huber", "Karl").Value,
            ],
            specialDocuments: [DriverSpecialDocument.Create("ADR-Schein").Value],
            phoneNumbers:
            [
                DriverPhoneNumber.Create("+43 1 111", "Privat").Value,
                DriverPhoneNumber.Create("+43 1 222", "Mobil").Value,
            ]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Relatives.Select(r => (r.Sequence, r.FirstName))
            .Should().Equal((1, "Anna"), (2, "Karl"));
        result.Value.SpecialDocuments.Single().Sequence.Should().Be(1);
        result.Value.PhoneNumbers.Select(p => p.Number).Should().Equal("+43 1 111", "+43 1 222");
    }

    [Fact]
    public void DriverCreate_WithoutOptionalData_UsesEmptyValues()
    {
        Driver driver = Driver.Create("Max", "Fahrer", Classes, ValidUntil, null).Value;

        driver.Documents.Should().Be(DriverDocumentsInfo.Empty);
        driver.ClothingSizes.Should().Be(DriverClothingSizes.Empty);
        driver.Relatives.Should().BeEmpty();
        driver.SpecialDocuments.Should().BeEmpty();
        driver.PhoneNumbers.Should().BeEmpty();
    }

    [Fact]
    public void DriverCreate_TooManyRelatives_Fails()
    {
        DriverRelative[] relatives = Enumerable.Range(1, Driver.MaxRelatives + 1)
            .Select(i => DriverRelative.Create($"Name{i}").Value)
            .ToArray();

        Result<Driver> result = Driver.Create("Max", "Fahrer", Classes, ValidUntil, null, relatives: relatives);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.TooManyRelatives");
    }

    [Fact]
    public void DriverUpdate_ReplacesCollectionsAndRenumbers()
    {
        Driver driver = Driver.Create(
            "Max", "Fahrer", Classes, ValidUntil, null,
            relatives: [DriverRelative.Create("Alt", "Eintrag").Value],
            phoneNumbers: [DriverPhoneNumber.Create("+43 1 111").Value]).Value;

        Result<Driver> updated = driver.Update(
            "Max", "Fahrer", Classes, ValidUntil, null,
            relatives:
            [
                DriverRelative.Create("Neu", "Eins").Value,
                DriverRelative.Create("Neu", "Zwei").Value,
            ]);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Relatives.Select(r => (r.Sequence, r.LastName, r.FirstName))
            .Should().Equal((1, "Neu", "Eins"), (2, "Neu", "Zwei"));
        updated.Value.PhoneNumbers.Should().BeEmpty();
    }

    [Fact]
    public void DriverUpdate_WithoutOptionalData_ClearsEverything()
    {
        Driver driver = Driver.Create(
            "Max", "Fahrer", Classes, ValidUntil, null,
            documents: DriverDocumentsInfo.Create(licenseNumber: "B123").Value,
            clothingSizes: DriverClothingSizes.Create(clothingSize: "52").Value,
            specialDocuments: [DriverSpecialDocument.Create("ADR").Value]).Value;

        Driver updated = driver.Update("Max", "Fahrer", Classes, ValidUntil, null).Value;

        updated.Documents.Should().Be(DriverDocumentsInfo.Empty);
        updated.ClothingSizes.Should().Be(DriverClothingSizes.Empty);
        updated.SpecialDocuments.Should().BeEmpty();
    }
}
