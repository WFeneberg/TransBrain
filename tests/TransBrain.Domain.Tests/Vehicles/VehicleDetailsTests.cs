using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleDetailsTests
{
    [Fact]
    public void Create_NoArguments_EqualsEmpty()
    {
        Result<VehicleDetails> result = VehicleDetails.Create();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(VehicleDetails.Empty);
    }

    [Fact]
    public void Create_TextValues_TrimsBlankToNullAndUpperCasesChassisNumber()
    {
        VehicleDetails details = VehicleDetails.Create(
            make: "  MAN ", model: "   ", chassisNumber: " wma06xzz ", notes: "").Value;

        details.Make.Should().Be("MAN");
        details.Model.Should().BeNull();
        details.ChassisNumber.Should().Be("WMA06XZZ");
        details.Notes.Should().BeNull();
    }

    [Fact]
    public void Create_ManufacturedOn_NormalizesToFirstOfMonth()
    {
        VehicleDetails details = VehicleDetails.Create(manufacturedOn: new DateOnly(2021, 5, 17)).Value;

        details.ManufacturedOn.Should().Be(new DateOnly(2021, 5, 1));
    }

    [Fact]
    public void Create_ManufacturedBeforeMinimumYear_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(manufacturedOn: new DateOnly(1899, 12, 1));

        result.Error!.Code.Should().Be("Vehicle.ManufacturedOnTooEarly");
    }

    [Fact]
    public void Create_TooLongName_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(make: new string('M', VehicleDetails.MaxNameLength + 1));

        result.Error!.Code.Should().Be("Vehicle.TextTooLong");
    }

    [Fact]
    public void Create_TooLongIdentifier_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(
            engineNumber: new string('E', VehicleDetails.MaxIdentifierLength + 1));

        result.Error!.Code.Should().Be("Vehicle.IdentifierTooLong");
    }

    [Fact]
    public void Create_TooLongNotes_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(notes: new string('n', VehicleDetails.MaxNotesLength + 1));

        result.Error!.Code.Should().Be("Vehicle.NotesTooLong");
    }

    [Fact]
    public void Create_UndefinedClass_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(vehicleClass: (VehicleClass)99);

        result.Error!.Code.Should().Be("Vehicle.UnknownClass");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(VehicleDetails.MaxPowerKw + 1)]
    public void Create_PowerOutOfRange_ReturnsValidationError(int powerKw)
    {
        Result<VehicleDetails> result = VehicleDetails.Create(powerKw: powerKw);

        result.Error!.Code.Should().Be("Vehicle.PowerOutOfRange");
    }

    [Fact]
    public void Create_DimensionOutOfRange_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(widthMm: VehicleDetails.MaxWidthMm + 1);

        result.Error!.Code.Should().Be("Vehicle.DimensionOutOfRange");
    }

    [Fact]
    public void Create_WeightOutOfRange_ReturnsValidationError()
    {
        Result<VehicleDetails> result = VehicleDetails.Create(maxFifthWheelLoadKg: 0);

        result.Error!.Code.Should().Be("Vehicle.WeightOutOfRange");
    }

    [Theory]
    [InlineData(8_000, 8_000)]
    [InlineData(9_000, 8_000)]
    public void Create_GrossWeightNotAboveCurbWeight_ReturnsValidationError(int curb, int gross)
    {
        Result<VehicleDetails> result = VehicleDetails.Create(curbWeightKg: curb, grossWeightKg: gross);

        result.Error!.Code.Should().Be("Vehicle.GrossWeightNotAboveCurbWeight");
    }

    [Fact]
    public void Create_FullValidData_KeepsAllValues()
    {
        VehicleDetails details = VehicleDetails.Create(
            isLicensePlateConfidential: true,
            powerKw: 375,
            vehicleClass: VehicleClass.Tank,
            lengthMm: 6_100,
            heightMm: 3_950,
            curbWeightKg: 8_200,
            grossWeightKg: 18_000,
            maxFifthWheelLoadKg: 11_500).Value;

        details.IsLicensePlateConfidential.Should().BeTrue();
        details.PowerKw.Should().Be(375);
        details.VehicleClass.Should().Be(VehicleClass.Tank);
        details.LengthMm.Should().Be(6_100);
        details.HeightMm.Should().Be(3_950);
        details.GrossWeightKg.Should().Be(18_000);
        details.MaxFifthWheelLoadKg.Should().Be(11_500);
    }
}
