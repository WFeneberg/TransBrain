using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

public sealed class Vehicle
{
    public const int MaxModifiedByLength = 100;

    private Vehicle()
    {
        LicensePlate = null!;
        Details = VehicleDetails.Empty;
        Equipment = VehicleEquipment.Empty;
        Commercial = VehicleCommercialInfo.Empty;
    }

    private Vehicle(
        Guid id,
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue,
        VehicleStatus status,
        VehicleDetails details,
        VehicleEquipment equipment,
        VehicleCommercialInfo commercial,
        Guid? defaultDriverId)
    {
        Id = id;
        LicensePlate = licensePlate;
        Type = type;
        PayloadKg = payloadKg;
        LoadMeters = loadMeters;
        NextInspectionDue = nextInspectionDue;
        Status = status;
        Details = details;
        Equipment = equipment;
        Commercial = commercial;
        DefaultDriverId = defaultDriverId;
    }

    public Guid Id { get; private set; }

    public LicensePlate LicensePlate { get; private set; }

    public VehicleType Type { get; private set; }

    public int PayloadKg { get; private set; }

    public decimal LoadMeters { get; private set; }

    public DateOnly NextInspectionDue { get; private set; }

    public VehicleStatus Status { get; private set; }

    public VehicleDetails Details { get; private set; }

    public VehicleEquipment Equipment { get; private set; }

    public VehicleCommercialInfo Commercial { get; private set; }

    /// <summary>The driver who usually drives this vehicle ("Stammfahrer").</summary>
    public Guid? DefaultDriverId { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    /// <summary>Login name of whoever last changed the vehicle.</summary>
    public string? ModifiedBy { get; private set; }

    public static Result<Vehicle> Create(
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue,
        VehicleDetails? details = null,
        Guid? defaultDriverId = null,
        VehicleEquipment? equipment = null,
        VehicleCommercialInfo? commercial = null)
    {
        Result<Unit> validation = Validate(payloadKg, loadMeters);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        return new Vehicle(
            Guid.CreateVersion7(),
            licensePlate,
            type,
            payloadKg,
            loadMeters,
            nextInspectionDue,
            VehicleStatus.Available,
            details ?? VehicleDetails.Empty,
            equipment ?? VehicleEquipment.Empty,
            commercial ?? VehicleCommercialInfo.Empty,
            defaultDriverId);
    }

    /// <param name="equipment">Replaces the whole equipment; null clears it like <see cref="VehicleEquipment.Empty"/>.</param>
    /// <param name="commercial">Replaces the whole commercial data; null clears it.</param>
    public Result<Vehicle> Update(
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue,
        VehicleDetails details,
        Guid? defaultDriverId,
        VehicleEquipment? equipment = null,
        VehicleCommercialInfo? commercial = null)
    {
        Result<Unit> validation = Validate(payloadKg, loadMeters);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        LicensePlate = licensePlate;
        Type = type;
        PayloadKg = payloadKg;
        LoadMeters = loadMeters;
        NextInspectionDue = nextInspectionDue;
        Details = details;
        Equipment = equipment ?? VehicleEquipment.Empty;
        Commercial = commercial ?? VehicleCommercialInfo.Empty;
        DefaultDriverId = defaultDriverId;

        return this;
    }

    public void SendToWorkshop()
    {
        if (Status == VehicleStatus.Available)
        {
            Status = VehicleStatus.InWorkshop;
        }
    }

    /// <remarks>Deliberately refuses to revive a decommissioned vehicle.</remarks>
    public void ReturnToService()
    {
        if (Status == VehicleStatus.InWorkshop)
        {
            Status = VehicleStatus.Available;
        }
    }

    public void Decommission() => Status = VehicleStatus.Decommissioned;

    /// <summary>
    /// Moves the vehicle to <paramref name="target"/>. Unlike the silent transition methods above,
    /// this reports the one forbidden move — reviving a decommissioned vehicle — as a conflict,
    /// because a user asked for it explicitly and must learn that it did not happen.
    /// </summary>
    public Result<Vehicle> ChangeStatus(VehicleStatus target)
    {
        if (!Enum.IsDefined(target))
        {
            return Error.Validation("Vehicle.UnknownStatus", $"'{target}' is not a known vehicle status.");
        }

        if (Status == target)
        {
            return this;
        }

        if (Status == VehicleStatus.Decommissioned)
        {
            return Error.Conflict(
                "Vehicle.Decommissioned", "A decommissioned vehicle cannot be returned to service.");
        }

        switch (target)
        {
            case VehicleStatus.InWorkshop:
                SendToWorkshop();
                break;
            case VehicleStatus.Available:
                ReturnToService();
                break;
            default:
                Decommission();
                break;
        }

        return this;
    }

    public void RecordModification(string? modifiedBy, DateTimeOffset modifiedAt)
    {
        string? name = OptionalText.Normalize(modifiedBy);
        ModifiedBy = name is { Length: > MaxModifiedByLength } ? name[..MaxModifiedByLength] : name;
        ModifiedAt = modifiedAt;
    }

    private static Result<Unit> Validate(int payloadKg, decimal loadMeters)
    {
        if (payloadKg <= 0)
        {
            return Error.Validation("Vehicle.PayloadKgNotPositive", "Payload must be greater than zero.");
        }

        if (loadMeters <= 0m)
        {
            return Error.Validation("Vehicle.LoadMetersNotPositive", "Load meters must be greater than zero.");
        }

        return Unit.Value;
    }
}
