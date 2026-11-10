using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// The fixtures and loose load-securing gear a vehicle carries ("Ausstattung"). Dispatch uses it to
/// tell whether a vehicle can take a particular load, e.g. dangerous goods or stone slabs.
/// Every value is optional; a count of <c>null</c> means "not recorded", <c>0</c> means "none".
/// </summary>
public sealed record VehicleEquipment
{
    public const int MaxTankVolumeLiters = 100_000;
    public const int MaxCount = 999;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static VehicleEquipment Empty => new();

    private VehicleEquipment()
    {
    }

    /// <summary>Partition wall dividing the loading space ("Trennwand").</summary>
    public bool HasPartitionWall { get; private init; }

    /// <summary>Basket for carrying exchange pallets ("Palettenkorb").</summary>
    public bool HasPalletBasket { get; private init; }

    /// <summary>Carries the ADR equipment kit for dangerous goods ("Gefahrgutausrüstung").</summary>
    public bool HasDangerousGoodsEquipment { get; private init; }

    /// <summary>The vehicle itself is approved for dangerous goods ("Gefahrguttauglich", ADR).</summary>
    public bool IsDangerousGoodsApproved { get; private init; }

    public bool HasLiftAxle { get; private init; }

    /// <summary>Second loading level ("Doppelstock").</summary>
    public bool HasDoubleDeck { get; private init; }

    /// <summary>Beams for building a second loading level ("Doppelstockbalken").</summary>
    public bool HasDoubleDeckBeams { get; private init; }

    public bool HasSmallLadder { get; private init; }

    public bool HasLargeLadder { get; private init; }

    public int? TankVolumeLiters { get; private init; }

    public int? InteriorLengthMm { get; private init; }

    public int? InteriorWidthMm { get; private init; }

    public int? InteriorHeightMm { get; private init; }

    /// <summary>Number of support poles ("Stützstangen").</summary>
    public int? SupportPoleCount { get; private init; }

    /// <summary>Number of load bars ("Spannlatten").</summary>
    public int? LoadBarCount { get; private init; }

    /// <summary>Number of lashing straps ("Spanngurte").</summary>
    public int? LashingStrapCount { get; private init; }

    /// <summary>Number of pallets carried along, e.g. for pallet exchange.</summary>
    public int? PalletCount { get; private init; }

    /// <summary>Number of chain tensioners ("Kettenspanner").</summary>
    public int? ChainTensionerCount { get; private init; }

    /// <summary>Number of A-frame racks for stone slabs ("Marmorböcke").</summary>
    public int? MarbleRackCount { get; private init; }

    public MarbleRackMaterial? MarbleRackMaterial { get; private init; }

    public static Result<VehicleEquipment> Create(
        bool hasPartitionWall = false,
        bool hasPalletBasket = false,
        bool hasDangerousGoodsEquipment = false,
        bool isDangerousGoodsApproved = false,
        bool hasLiftAxle = false,
        bool hasDoubleDeck = false,
        bool hasDoubleDeckBeams = false,
        bool hasSmallLadder = false,
        bool hasLargeLadder = false,
        int? tankVolumeLiters = null,
        int? interiorLengthMm = null,
        int? interiorWidthMm = null,
        int? interiorHeightMm = null,
        int? supportPoleCount = null,
        int? loadBarCount = null,
        int? lashingStrapCount = null,
        int? palletCount = null,
        int? chainTensionerCount = null,
        int? marbleRackCount = null,
        MarbleRackMaterial? marbleRackMaterial = null)
    {
        VehicleEquipment equipment = new()
        {
            HasPartitionWall = hasPartitionWall,
            HasPalletBasket = hasPalletBasket,
            HasDangerousGoodsEquipment = hasDangerousGoodsEquipment,
            IsDangerousGoodsApproved = isDangerousGoodsApproved,
            HasLiftAxle = hasLiftAxle,
            HasDoubleDeck = hasDoubleDeck,
            HasDoubleDeckBeams = hasDoubleDeckBeams,
            HasSmallLadder = hasSmallLadder,
            HasLargeLadder = hasLargeLadder,
            TankVolumeLiters = tankVolumeLiters,
            InteriorLengthMm = interiorLengthMm,
            InteriorWidthMm = interiorWidthMm,
            InteriorHeightMm = interiorHeightMm,
            SupportPoleCount = supportPoleCount,
            LoadBarCount = loadBarCount,
            LashingStrapCount = lashingStrapCount,
            PalletCount = palletCount,
            ChainTensionerCount = chainTensionerCount,
            MarbleRackCount = marbleRackCount,
            MarbleRackMaterial = marbleRackMaterial,
        };

        Error? error = equipment.Validate();
        return error is null ? equipment : error;
    }

    private Error? Validate()
    {
        if (TankVolumeLiters is { } volume && (volume < 1 || volume > MaxTankVolumeLiters))
        {
            return Error.Validation(
                "Vehicle.TankVolumeOutOfRange", $"Tank volume must be between 1 and {MaxTankVolumeLiters} l.");
        }

        if (OutOfRange(InteriorLengthMm, VehicleDetails.MaxLengthMm)
            || OutOfRange(InteriorWidthMm, VehicleDetails.MaxWidthMm)
            || OutOfRange(InteriorHeightMm, VehicleDetails.MaxHeightMm))
        {
            return Error.Validation(
                "Vehicle.InteriorDimensionOutOfRange",
                $"Interior length must be between 1 and {VehicleDetails.MaxLengthMm} mm, " +
                $"width and height between 1 and {VehicleDetails.MaxWidthMm} mm.");
        }

        if (new[] { SupportPoleCount, LoadBarCount, LashingStrapCount, PalletCount, ChainTensionerCount, MarbleRackCount }
            .Any(count => count is < 0 or > MaxCount))
        {
            return Error.Validation("Vehicle.CountOutOfRange", $"Counts must be between 0 and {MaxCount}.");
        }

        if (MarbleRackMaterial is { } material && !Enum.IsDefined(material))
        {
            return Error.Validation(
                "Vehicle.UnknownMarbleRackMaterial", $"'{material}' is not a known marble rack material.");
        }

        return null;
    }

    private static bool OutOfRange(int? value, int max) => value is { } number && (number < 1 || number > max);
}
