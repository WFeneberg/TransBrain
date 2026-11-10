using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleEquipmentTests
{
    private static Vehicle NewVehicle(VehicleEquipment? equipment = null) =>
        Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.SemiTrailer, 3_000, 4.0m, new DateOnly(2027, 3, 31),
            equipment: equipment).Value;

    [Fact]
    public void Create_NoArguments_EqualsEmpty()
    {
        Result<VehicleEquipment> result = VehicleEquipment.Create();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(VehicleEquipment.Empty);
    }

    [Fact]
    public void Create_FullValidData_KeepsAllValues()
    {
        VehicleEquipment equipment = VehicleEquipment.Create(
            hasPartitionWall: true,
            hasPalletBasket: true,
            hasDangerousGoodsEquipment: true,
            isDangerousGoodsApproved: true,
            hasLiftAxle: true,
            hasDoubleDeck: true,
            hasDoubleDeckBeams: true,
            hasSmallLadder: true,
            hasLargeLadder: true,
            tankVolumeLiters: 1_200,
            interiorLengthMm: 13_600,
            interiorWidthMm: 2_480,
            interiorHeightMm: 2_700,
            supportPoleCount: 4,
            loadBarCount: 0,
            lashingStrapCount: 20,
            palletCount: 2,
            chainTensionerCount: 6,
            marbleRackCount: 3,
            marbleRackMaterial: MarbleRackMaterial.Aluminium).Value;

        equipment.HasPartitionWall.Should().BeTrue();
        equipment.HasLargeLadder.Should().BeTrue();
        equipment.IsDangerousGoodsApproved.Should().BeTrue();
        equipment.TankVolumeLiters.Should().Be(1_200);
        equipment.InteriorLengthMm.Should().Be(13_600);
        equipment.InteriorWidthMm.Should().Be(2_480);
        equipment.InteriorHeightMm.Should().Be(2_700);
        equipment.LoadBarCount.Should().Be(0);
        equipment.LashingStrapCount.Should().Be(20);
        equipment.MarbleRackCount.Should().Be(3);
        equipment.MarbleRackMaterial.Should().Be(MarbleRackMaterial.Aluminium);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(VehicleEquipment.MaxTankVolumeLiters + 1)]
    public void Create_TankVolumeOutOfRange_ReturnsValidationError(int liters)
    {
        Result<VehicleEquipment> result = VehicleEquipment.Create(tankVolumeLiters: liters);

        result.Error!.Code.Should().Be("Vehicle.TankVolumeOutOfRange");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(VehicleDetails.MaxLengthMm + 1, null, null)]
    [InlineData(null, VehicleDetails.MaxWidthMm + 1, null)]
    [InlineData(null, null, 0)]
    public void Create_InteriorDimensionOutOfRange_ReturnsValidationError(int? length, int? width, int? height)
    {
        Result<VehicleEquipment> result = VehicleEquipment.Create(
            interiorLengthMm: length, interiorWidthMm: width, interiorHeightMm: height);

        result.Error!.Code.Should().Be("Vehicle.InteriorDimensionOutOfRange");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(VehicleEquipment.MaxCount + 1)]
    public void Create_CountOutOfRange_ReturnsValidationError(int count)
    {
        VehicleEquipment.Create(supportPoleCount: count).Error!.Code.Should().Be("Vehicle.CountOutOfRange");
        VehicleEquipment.Create(marbleRackCount: count).Error!.Code.Should().Be("Vehicle.CountOutOfRange");
    }

    [Fact]
    public void Create_UndefinedMarbleRackMaterial_ReturnsValidationError()
    {
        Result<VehicleEquipment> result = VehicleEquipment.Create(marbleRackMaterial: (MarbleRackMaterial)42);

        result.Error!.Code.Should().Be("Vehicle.UnknownMarbleRackMaterial");
    }

    [Fact]
    public void Empty_ReturnsAFreshInstanceEachTime()
    {
        VehicleEquipment.Empty.Should().NotBeSameAs(VehicleEquipment.Empty);
        VehicleEquipment.Empty.Should().Be(VehicleEquipment.Empty);
    }

    [Fact]
    public void VehicleCreate_WithoutEquipment_HasEmptyEquipment()
    {
        NewVehicle().Equipment.Should().Be(VehicleEquipment.Empty);
    }

    [Fact]
    public void VehicleUpdate_NewEquipment_ReplacesIt()
    {
        Vehicle vehicle = NewVehicle(VehicleEquipment.Create(hasLiftAxle: true).Value);
        VehicleEquipment replacement = VehicleEquipment.Create(palletCount: 5).Value;

        vehicle.Update(
            vehicle.LicensePlate, vehicle.Type, vehicle.PayloadKg, vehicle.LoadMeters,
            vehicle.NextInspectionDue, vehicle.Details, vehicle.DefaultDriverId, replacement);

        vehicle.Equipment.Should().Be(replacement);
        vehicle.Equipment.HasLiftAxle.Should().BeFalse();
    }

    [Fact]
    public void VehicleUpdate_NullEquipment_ClearsIt()
    {
        Vehicle vehicle = NewVehicle(VehicleEquipment.Create(hasLiftAxle: true, palletCount: 5).Value);

        vehicle.Update(
            vehicle.LicensePlate, vehicle.Type, vehicle.PayloadKg, vehicle.LoadMeters,
            vehicle.NextInspectionDue, vehicle.Details, vehicle.DefaultDriverId);

        vehicle.Equipment.Should().Be(VehicleEquipment.Empty);
    }
}
