using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleCatalogEntryTests
{
    [Fact]
    public void Create_ValidValues_TrimsAndKeepsThem()
    {
        VehicleCatalogEntry entry = VehicleCatalogEntry.Create(
            VehicleCatalogKind.CarrierGroup, " NV-Sued_1 ", " Nahverkehr Süd ", isActive: true).Value;

        entry.Id.Should().NotBe(Guid.Empty);
        entry.Kind.Should().Be(VehicleCatalogKind.CarrierGroup);
        entry.Code.Should().Be("NV-Sued_1");
        entry.Name.Should().Be("Nahverkehr Süd");
        entry.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("With space")]
    [InlineData("Umlaut-Ä")]
    [InlineData("a/b")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void Create_InvalidCode_ReturnsValidationError(string code)
    {
        Result<VehicleCatalogEntry> result = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, code, "Name", isActive: true);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("VehicleCatalog.InvalidCode");
    }

    [Fact]
    public void Create_NullCode_ReturnsValidationError()
    {
        Result<VehicleCatalogEntry> result = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, null!, "Name", isActive: true);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidCode");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyName_ReturnsValidationError(string name)
    {
        Result<VehicleCatalogEntry> result = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, "Code", name, isActive: true);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidName");
    }

    [Fact]
    public void Create_TooLongName_ReturnsValidationError()
    {
        Result<VehicleCatalogEntry> result = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, "Code", new string('n', VehicleCatalogEntry.MaxNameLength + 1), true);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidName");
    }

    [Fact]
    public void Create_UndefinedKind_ReturnsValidationError()
    {
        Result<VehicleCatalogEntry> result = VehicleCatalogEntry.Create((VehicleCatalogKind)99, "Code", "Name", true);

        result.Error!.Code.Should().Be("VehicleCatalog.UnknownKind");
    }

    [Fact]
    public void Update_ValidValues_ChangesNameAndActiveButNotCode()
    {
        VehicleCatalogEntry entry = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, "Tank", "Tank", isActive: true).Value;

        Result<VehicleCatalogEntry> result = entry.Update(" Tankwagen ", isActive: false);

        result.IsSuccess.Should().BeTrue();
        entry.Code.Should().Be("Tank");
        entry.Name.Should().Be("Tankwagen");
        entry.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Update_EmptyName_ReturnsValidationErrorAndKeepsValues()
    {
        VehicleCatalogEntry entry = VehicleCatalogEntry.Create(
            VehicleCatalogKind.VehicleClass, "Tank", "Tank", isActive: true).Value;

        Result<VehicleCatalogEntry> result = entry.Update(" ", isActive: false);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidName");
        entry.Name.Should().Be("Tank");
        entry.IsActive.Should().BeTrue();
    }
}
