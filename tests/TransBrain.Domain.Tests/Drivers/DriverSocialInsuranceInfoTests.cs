using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Domain.Tests.Drivers;

public class DriverSocialInsuranceInfoTests
{
    private static readonly DateOnly ValidUntil = new(2028, 6, 30);
    private static readonly LicenseClass[] Classes = [LicenseClass.CE];

    [Fact]
    public void Create_AllValues_NormalizesTextAndUpperCasesBicAndIban()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            dateOfBirth: new DateOnly(1980, 5, 17),
            placeOfBirth: "  Linz ",
            nationality: " Österreich ",
            socialInsuranceNumber: " 1234 170580 ",
            entryDate: new DateOnly(2014, 10, 1),
            healthInsurance: "ÖGK Oberösterreich\nGruberstraße 77, 4021 Linz",
            bankName: " Raiffeisenbank ",
            bankCode: "34000",
            accountNumber: "00012345678",
            bic: " rzooat2l ",
            iban: " at61 1904 3002 3457 3201 ",
            familyDoctor: "Dr. Maria Huber\nHauptplatz 3, 4020 Linz",
            familyDoctorPhone: " +43 732 123456 ");

        result.IsSuccess.Should().BeTrue();
        result.Value.DateOfBirth.Should().Be(new DateOnly(1980, 5, 17));
        result.Value.PlaceOfBirth.Should().Be("Linz");
        result.Value.Nationality.Should().Be("Österreich");
        result.Value.SocialInsuranceNumber.Should().Be("1234 170580");
        result.Value.EntryDate.Should().Be(new DateOnly(2014, 10, 1));
        result.Value.BankName.Should().Be("Raiffeisenbank");
        result.Value.Bic.Should().Be("RZOOAT2L");
        result.Value.Iban.Should().Be("AT61 1904 3002 3457 3201");
        result.Value.FamilyDoctorPhone.Should().Be("+43 732 123456");
    }

    [Fact]
    public void Create_BlankTexts_BecomeNull()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            placeOfBirth: "   ", healthInsurance: "", familyDoctor: " ");

        result.IsSuccess.Should().BeTrue();
        result.Value.PlaceOfBirth.Should().BeNull();
        result.Value.HealthInsurance.Should().BeNull();
        result.Value.FamilyDoctor.Should().BeNull();
    }

    [Fact]
    public void Create_NoValues_EqualsEmpty()
    {
        DriverSocialInsuranceInfo.Create().Value.Should().Be(DriverSocialInsuranceInfo.Empty);
    }

    [Fact]
    public void Create_DateOfBirthBeforeMinimumYear_ReturnsValidationError()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            dateOfBirth: new DateOnly(1899, 12, 31));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.DateOfBirthTooEarly");
    }

    [Fact]
    public void Create_EntryDateBeforeMinimumYear_ReturnsValidationError()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            entryDate: new DateOnly(1899, 12, 31));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.EntryDateTooEarly");
    }

    [Fact]
    public void Create_TooLongSocialInsuranceNumber_ReturnsValidationError()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            socialInsuranceNumber: new string('1', DriverSocialInsuranceInfo.MaxSocialInsuranceNumberLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.SocialInsuranceTextTooLong");
    }

    [Fact]
    public void Create_TooLongIban_ReturnsValidationError()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            iban: new string('1', DriverSocialInsuranceInfo.MaxIbanLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.BankDetailsTooLong");
    }

    [Fact]
    public void Create_TooLongFamilyDoctorPhone_ReturnsValidationError()
    {
        Result<DriverSocialInsuranceInfo> result = DriverSocialInsuranceInfo.Create(
            familyDoctorPhone: new string('1', DriverSocialInsuranceInfo.MaxFamilyDoctorPhoneLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FamilyDoctorTooLong");
    }

    [Fact]
    public void DriverCreate_WithoutSocialInsurance_UsesEmpty()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        driver.SocialInsurance.Should().Be(DriverSocialInsuranceInfo.Empty);
    }

    [Fact]
    public void DriverUpdate_WithSocialInsurance_ReplacesIt()
    {
        Driver driver = Driver.Create(
            "Frank", "Fahrer", Classes, ValidUntil, null,
            DriverSocialInsuranceInfo.Create(placeOfBirth: "Linz").Value).Value;
        DriverSocialInsuranceInfo replacement =
            DriverSocialInsuranceInfo.Create(bankName: "Raiffeisenbank").Value;

        Result<Driver> result = driver.Update("Frank", "Fahrer", Classes, ValidUntil, null, replacement);

        result.IsSuccess.Should().BeTrue();
        driver.SocialInsurance.Should().Be(replacement);
        driver.SocialInsurance.PlaceOfBirth.Should().BeNull();
    }

    [Fact]
    public void DriverUpdate_WithoutSocialInsurance_ClearsIt()
    {
        Driver driver = Driver.Create(
            "Frank", "Fahrer", Classes, ValidUntil, null,
            DriverSocialInsuranceInfo.Create(placeOfBirth: "Linz").Value).Value;

        driver.Update("Frank", "Fahrer", Classes, ValidUntil, null);

        driver.SocialInsurance.Should().Be(DriverSocialInsuranceInfo.Empty);
    }
}
