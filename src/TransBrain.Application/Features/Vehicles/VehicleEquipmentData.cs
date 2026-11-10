using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles;

/// <summary>
/// Wire shape of <see cref="VehicleEquipment"/>, used in both the vehicle write bodies and
/// <see cref="VehicleResponse"/>. <see cref="MarbleRackMaterial"/> is <c>Steel</c>, <c>Aluminium</c> or null.
/// </summary>
public sealed record VehicleEquipmentData(
    bool HasPartitionWall = false,
    bool HasPalletBasket = false,
    bool HasDangerousGoodsEquipment = false,
    bool IsDangerousGoodsApproved = false,
    bool HasLiftAxle = false,
    bool HasDoubleDeck = false,
    bool HasDoubleDeckBeams = false,
    bool HasSmallLadder = false,
    bool HasLargeLadder = false,
    int? TankVolumeLiters = null,
    int? InteriorLengthMm = null,
    int? InteriorWidthMm = null,
    int? InteriorHeightMm = null,
    int? SupportPoleCount = null,
    int? LoadBarCount = null,
    int? LashingStrapCount = null,
    int? PalletCount = null,
    int? ChainTensionerCount = null,
    int? MarbleRackCount = null,
    string? MarbleRackMaterial = null)
{
    public static VehicleEquipmentData From(VehicleEquipment equipment) => new(
        equipment.HasPartitionWall,
        equipment.HasPalletBasket,
        equipment.HasDangerousGoodsEquipment,
        equipment.IsDangerousGoodsApproved,
        equipment.HasLiftAxle,
        equipment.HasDoubleDeck,
        equipment.HasDoubleDeckBeams,
        equipment.HasSmallLadder,
        equipment.HasLargeLadder,
        equipment.TankVolumeLiters,
        equipment.InteriorLengthMm,
        equipment.InteriorWidthMm,
        equipment.InteriorHeightMm,
        equipment.SupportPoleCount,
        equipment.LoadBarCount,
        equipment.LashingStrapCount,
        equipment.PalletCount,
        equipment.ChainTensionerCount,
        equipment.MarbleRackCount,
        equipment.MarbleRackMaterial?.ToString());
}
