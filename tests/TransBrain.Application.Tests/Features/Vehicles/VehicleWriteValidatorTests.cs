using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class VehicleWriteValidatorTests
{
    private static CreateVehicleCommand ValidCommand => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    [Fact]
    public void Validate_MinimalCommand_IsValid()
    {
        new CreateVehicleCommandValidator().Validate(ValidCommand).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SeveralInvalidDetails_ReportsEachFieldAtOnce()
    {
        ValidationResult result = new CreateVehicleCommandValidator().Validate(ValidCommand with
        {
            Make = new string('M', VehicleDetails.MaxNameLength + 1),
            ChassisNumber = new string('C', VehicleDetails.MaxIdentifierLength + 1),
            Notes = new string('n', VehicleDetails.MaxNotesLength + 1),
            ManufacturedOn = new DateOnly(1899, 1, 1),
            VehicleClass = "Spaceship",
            PowerKw = 0,
            LengthMm = VehicleDetails.MaxLengthMm + 1,
            MaxFifthWheelLoadKg = VehicleDetails.MaxWeightKg + 1,
        });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
        [
            "Make", "ChassisNumber", "Notes", "ManufacturedOn", "VehicleClass", "PowerKw", "LengthMm",
            "MaxFifthWheelLoadKg",
        ]);
    }

    [Fact]
    public void Validate_GrossWeightNotAboveCurbWeight_ReportsGrossWeight()
    {
        ValidationResult result = new UpdateVehicleCommandValidator().Validate(new UpdateVehicleCommand(
            Guid.CreateVersion7(), "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31),
            CurbWeightKg: 5_000, GrossWeightKg: 4_000));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("GrossWeightKg");
    }

    [Fact]
    public void Validate_CaseInsensitiveVehicleClass_IsValid()
    {
        new CreateVehicleCommandValidator().Validate(ValidCommand with { VehicleClass = "carTRANSPORTER" })
            .IsValid.Should().BeTrue();
    }
}
