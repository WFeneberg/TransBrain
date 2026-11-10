using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleCommercialInfoTests
{
    private static Vehicle NewVehicle(VehicleCommercialInfo? commercial = null) =>
        Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31),
            commercial: commercial).Value;

    [Fact]
    public void Create_NoArguments_EqualsEmpty()
    {
        Result<VehicleCommercialInfo> result = VehicleCommercialInfo.Create();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(VehicleCommercialInfo.Empty);
    }

    [Fact]
    public void Create_FullValidData_KeepsAllValuesAndTrimsText()
    {
        VehicleCommercialInfo info = VehicleCommercialInfo.Create(
            purchasePrice: 125_000.50m,
            purchasedOn: new DateOnly(2023, 3, 15),
            purchaseOdometerKm: 0,
            firstRegisteredOn: new DateOnly(2022, 11, 30),
            usefulLifeYears: 8,
            usefulLifeKm: 900_000,
            costCenter: "  KST-4711 ",
            vehicleInsurer: " ALLIANZ ",
            vehicleInsurancePolicyNumbers: " POL-1\nPOL-2 ",
            transportInsurer: "UNIQA",
            transportInsurancePolicyNumbers: "TR-9").Value;

        info.PurchasePrice.Should().Be(125_000.50m);
        info.PurchasedOn.Should().Be(new DateOnly(2023, 3, 15));
        info.PurchaseOdometerKm.Should().Be(0);
        info.FirstRegisteredOn.Should().Be(new DateOnly(2022, 11, 30));
        info.UsefulLifeYears.Should().Be(8);
        info.UsefulLifeKm.Should().Be(900_000);
        info.CostCenter.Should().Be("KST-4711");
        info.VehicleInsurer.Should().Be("ALLIANZ");
        info.VehicleInsurancePolicyNumbers.Should().Be("POL-1\nPOL-2");
        info.TransportInsurer.Should().Be("UNIQA");
        info.TransportInsurancePolicyNumbers.Should().Be("TR-9");
    }

    [Fact]
    public void Create_BlankText_StoresNull()
    {
        VehicleCommercialInfo info = VehicleCommercialInfo.Create(
            costCenter: " ", vehicleInsurer: "", vehicleInsurancePolicyNumbers: "\n").Value;

        info.CostCenter.Should().BeNull();
        info.VehicleInsurer.Should().BeNull();
        info.VehicleInsurancePolicyNumbers.Should().BeNull();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10_000_000.01)]
    [InlineData(100.001)]
    public void Create_InvalidPurchasePrice_ReturnsValidationError(double price)
    {
        Result<VehicleCommercialInfo> result = VehicleCommercialInfo.Create(purchasePrice: (decimal)price);

        result.Error!.Code.Should().Be("Vehicle.PurchasePriceOutOfRange");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000_000)]
    public void Create_BoundaryPurchasePrice_Succeeds(int price)
    {
        VehicleCommercialInfo.Create(purchasePrice: price).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_000_000)]
    public void Create_PurchaseOdometerOutOfRange_ReturnsValidationError(int km)
    {
        VehicleCommercialInfo.Create(purchaseOdometerKm: km).Error!.Code
            .Should().Be("Vehicle.PurchaseOdometerOutOfRange");
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(51, null)]
    [InlineData(null, 0)]
    [InlineData(null, 10_000_000)]
    public void Create_UsefulLifeOutOfRange_ReturnsValidationError(int? years, int? km)
    {
        VehicleCommercialInfo.Create(usefulLifeYears: years, usefulLifeKm: km).Error!.Code
            .Should().Be("Vehicle.UsefulLifeOutOfRange");
    }

    [Fact]
    public void Create_DateBefore1900_ReturnsValidationError()
    {
        VehicleCommercialInfo.Create(firstRegisteredOn: new DateOnly(1899, 12, 31)).Error!.Code
            .Should().Be("Vehicle.CommercialDateTooEarly");
        VehicleCommercialInfo.Create(purchasedOn: new DateOnly(1899, 12, 31)).Error!.Code
            .Should().Be("Vehicle.CommercialDateTooEarly");
    }

    [Fact]
    public void Create_TooLongText_ReturnsValidationErrors()
    {
        VehicleCommercialInfo.Create(costCenter: new string('K', 31)).Error!.Code
            .Should().Be("Vehicle.CostCenterTooLong");
        VehicleCommercialInfo.Create(transportInsurer: new string('I', 21)).Error!.Code
            .Should().Be("Vehicle.InsurerTooLong");
        VehicleCommercialInfo.Create(vehicleInsurancePolicyNumbers: new string('P', 501)).Error!.Code
            .Should().Be("Vehicle.PolicyNumbersTooLong");
    }

    [Fact]
    public void VehicleCreate_WithoutCommercial_HasEmptyCommercial()
    {
        NewVehicle().Commercial.Should().Be(VehicleCommercialInfo.Empty);
    }

    [Fact]
    public void VehicleUpdate_NullCommercial_ClearsIt()
    {
        Vehicle vehicle = NewVehicle(VehicleCommercialInfo.Create(costCenter: "KST-1").Value);

        vehicle.Update(
            vehicle.LicensePlate, vehicle.Type, vehicle.PayloadKg, vehicle.LoadMeters, vehicle.NextInspectionDue,
            vehicle.Details, vehicle.DefaultDriverId);

        vehicle.Commercial.Should().Be(VehicleCommercialInfo.Empty);
    }

    [Fact]
    public void VehicleUpdate_NewCommercial_ReplacesIt()
    {
        Vehicle vehicle = NewVehicle(VehicleCommercialInfo.Create(costCenter: "KST-1").Value);
        VehicleCommercialInfo replacement = VehicleCommercialInfo.Create(usefulLifeYears: 6).Value;

        vehicle.Update(
            vehicle.LicensePlate, vehicle.Type, vehicle.PayloadKg, vehicle.LoadMeters, vehicle.NextInspectionDue,
            vehicle.Details, vehicle.DefaultDriverId, commercial: replacement);

        vehicle.Commercial.Should().Be(replacement);
    }
}
