using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// One workshop visit of a vehicle ("Wartung"): what was done, when, by which workshop and at what
/// cost. The entries of a vehicle form its maintenance history; <see cref="NextDueOn"/> carries the
/// follow-up date the workshop recommended.
/// </summary>
public sealed class VehicleMaintenance
{
    public const int MaxDescriptionLength = 1000;
    public const int MaxWorkshopLength = 200;
    public const int MaxOdometerKm = 9_999_999;
    public const decimal MaxCost = 10_000_000m;

    // EF Core materialization only. Every other construction goes through Create.
    private VehicleMaintenance()
    {
        Description = null!;
    }

    private VehicleMaintenance(Guid id, Guid vehicleId)
    {
        Id = id;
        VehicleId = vehicleId;
        Description = null!;
    }

    public Guid Id { get; private set; }

    public Guid VehicleId { get; private set; }

    public VehicleMaintenanceKind Kind { get; private set; }

    /// <summary>The day the work was carried out ("Durchgeführt am").</summary>
    public DateOnly PerformedOn { get; private set; }

    /// <summary>What was done ("Durchgeführte Arbeiten").</summary>
    public string Description { get; private set; }

    /// <summary>Who did the work ("Werkstatt"), own workshop included.</summary>
    public string? Workshop { get; private set; }

    /// <summary>Odometer reading at the visit ("Kilometerstand").</summary>
    public int? OdometerKm { get; private set; }

    /// <summary>Net cost of the visit ("Kosten").</summary>
    public decimal? Cost { get; private set; }

    /// <summary>When the next visit of this kind is due ("Nächste Fälligkeit").</summary>
    public DateOnly? NextDueOn { get; private set; }

    public DateTimeOffset ModifiedAt { get; private set; }

    /// <summary>Login name of whoever last changed the entry.</summary>
    public string? ModifiedBy { get; private set; }

    public static Result<VehicleMaintenance> Create(
        Guid vehicleId,
        VehicleMaintenanceKind kind,
        DateOnly performedOn,
        string description,
        string? workshop,
        int? odometerKm,
        decimal? cost,
        DateOnly? nextDueOn,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        return new VehicleMaintenance(Guid.CreateVersion7(), vehicleId)
            .Update(kind, performedOn, description, workshop, odometerKm, cost, nextDueOn, modifiedBy, modifiedAt);
    }

    public Result<VehicleMaintenance> Update(
        VehicleMaintenanceKind kind,
        DateOnly performedOn,
        string description,
        string? workshop,
        int? odometerKm,
        decimal? cost,
        DateOnly? nextDueOn,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        string? normalizedDescription = OptionalText.Normalize(description);
        string? normalizedWorkshop = OptionalText.Normalize(workshop);

        Error? error = Validate(
            kind, performedOn, normalizedDescription, normalizedWorkshop, odometerKm, cost, nextDueOn);
        if (error is not null)
        {
            return error;
        }

        Kind = kind;
        PerformedOn = performedOn;
        Description = normalizedDescription!;
        Workshop = normalizedWorkshop;
        OdometerKm = odometerKm;
        Cost = cost;
        NextDueOn = nextDueOn;

        string? name = OptionalText.Normalize(modifiedBy);
        ModifiedBy = name is { Length: > Vehicle.MaxModifiedByLength } ? name[..Vehicle.MaxModifiedByLength] : name;
        ModifiedAt = modifiedAt;

        return this;
    }

    private static Error? Validate(
        VehicleMaintenanceKind kind,
        DateOnly performedOn,
        string? description,
        string? workshop,
        int? odometerKm,
        decimal? cost,
        DateOnly? nextDueOn)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("VehicleMaintenance.UnknownKind", $"'{kind}' is not a known maintenance kind.");
        }

        if (performedOn.Year < VehicleDetails.MinManufactureYear)
        {
            return Error.Validation(
                "VehicleMaintenance.PerformedTooEarly",
                $"The maintenance date must not be before {VehicleDetails.MinManufactureYear}.");
        }

        if (description is null)
        {
            return Error.Validation(
                "VehicleMaintenance.DescriptionRequired", "The description must not be empty.");
        }

        if (description.Length > MaxDescriptionLength)
        {
            return Error.Validation(
                "VehicleMaintenance.DescriptionTooLong",
                $"The description must not exceed {MaxDescriptionLength} characters.");
        }

        if (workshop is { Length: > MaxWorkshopLength })
        {
            return Error.Validation(
                "VehicleMaintenance.WorkshopTooLong",
                $"The workshop must not exceed {MaxWorkshopLength} characters.");
        }

        if (odometerKm is < 0 or > MaxOdometerKm)
        {
            return Error.Validation(
                "VehicleMaintenance.OdometerOutOfRange",
                $"The odometer reading must be between 0 and {MaxOdometerKm} km.");
        }

        if (cost is < 0m or > MaxCost)
        {
            return Error.Validation(
                "VehicleMaintenance.CostOutOfRange", $"The cost must be between 0 and {MaxCost}.");
        }

        if (nextDueOn is { } due && due < performedOn)
        {
            return Error.Validation(
                "VehicleMaintenance.NextDueBeforePerformed",
                "The next due date must not be before the maintenance date.");
        }

        return null;
    }
}
