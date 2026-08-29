using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Domain.Tests.Drivers;

public class DriverTests
{
    private static readonly DateOnly ValidUntil = new(2028, 6, 30);
    private static readonly LicenseClass[] Classes = [LicenseClass.C, LicenseClass.CE];

    [Fact]
    public void Create_ValidArguments_ReturnsAvailableDriver()
    {
        Result<Driver> result = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.FirstName.Should().Be("Frank");
        result.Value.LastName.Should().Be("Fahrer");
        result.Value.LicenseClasses.Should().BeEquivalentTo(Classes);
        result.Value.Status.Should().Be(DriverStatus.Available);
        result.Value.ExternalUserId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankFirstName_ReturnsValidationError(string firstName)
    {
        Result<Driver> result = Driver.Create(firstName, "Fahrer", Classes, ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankLastName_ReturnsValidationError(string lastName)
    {
        Result<Driver> result = Driver.Create("Frank", lastName, Classes, ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LastNameRequired");
    }

    [Fact]
    public void Create_NoLicenseClasses_ReturnsValidationError()
    {
        Result<Driver> result = Driver.Create("Frank", "Fahrer", [], ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseClassRequired");
    }

    [Fact]
    public void Create_DuplicateLicenseClasses_StoresEachOnce()
    {
        Result<Driver> result = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.C, LicenseClass.C, LicenseClass.CE], ValidUntil, null);

        result.Value.LicenseClasses.Should().BeEquivalentTo([LicenseClass.C, LicenseClass.CE]);
    }

    [Fact]
    public void Create_NamesWithSurroundingWhitespace_StoresThemTrimmed()
    {
        Result<Driver> result = Driver.Create("  Frank  ", "  Fahrer ", Classes, ValidUntil, null);

        result.Value.FirstName.Should().Be("Frank");
        result.Value.LastName.Should().Be("Fahrer");
    }

    [Fact]
    public void CanDriveOn_AvailableAndLicenceStillValid_ReturnsTrue()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        driver.CanDriveOn(new DateOnly(2028, 6, 30)).Should().BeTrue();
    }

    [Fact]
    public void CanDriveOn_LicenceExpiredBeforeThatDate_ReturnsFalse()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        driver.CanDriveOn(new DateOnly(2028, 7, 1)).Should().BeFalse();
    }

    [Fact]
    public void CanDriveOn_DriverAbsent_ReturnsFalse()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.MarkAbsent();

        driver.CanDriveOn(new DateOnly(2027, 1, 1)).Should().BeFalse();
    }

    [Fact]
    public void MarkAvailable_AfterBeingAbsent_RestoresAvailability()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.MarkAbsent();

        driver.MarkAvailable();

        driver.Status.Should().Be(DriverStatus.Available);
    }

    [Fact]
    public void MarkAvailable_AfterDeactivation_LeavesDriverInactive()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;
        driver.Deactivate();

        driver.MarkAvailable();

        driver.Status.Should().Be(DriverStatus.Inactive);
    }

    [Fact]
    public void Update_ValidArguments_ReplacesNamesAndLicence()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        Result<Driver> result = driver.Update("Franz", "Fahrer", [LicenseClass.B], new DateOnly(2030, 1, 1), "sub-123");

        result.IsSuccess.Should().BeTrue();
        driver.FirstName.Should().Be("Franz");
        driver.LicenseClasses.Should().BeEquivalentTo([LicenseClass.B]);
        driver.LicenseValidUntil.Should().Be(new DateOnly(2030, 1, 1));
        driver.ExternalUserId.Should().Be("sub-123");
    }

    [Fact]
    public void Update_NoLicenseClasses_ReturnsValidationErrorAndLeavesDriverUnchanged()
    {
        Driver driver = Driver.Create("Frank", "Fahrer", Classes, ValidUntil, null).Value;

        Result<Driver> result = driver.Update("Franz", "Fahrer", [], ValidUntil, null);

        result.IsSuccess.Should().BeFalse();
        driver.FirstName.Should().Be("Frank");
        driver.LicenseClasses.Should().BeEquivalentTo(Classes);
    }
}
