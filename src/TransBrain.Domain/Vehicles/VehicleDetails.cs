using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// The descriptive master data of a vehicle: identification, technical data, dimensions and
/// weights. Every value is optional, because a haulier often learns them only bit by bit.
/// </summary>
public sealed record VehicleDetails
{
    public const int MaxNameLength = 100;
    public const int MaxIdentifierLength = 30;
    public const int MaxNotesLength = 2000;
    public const int MaxPowerKw = 2_000;
    public const int MaxLengthMm = 30_000;
    public const int MaxWidthMm = 5_000;
    public const int MaxHeightMm = 5_000;
    public const int MaxWeightKg = 100_000;
    public const int MinManufactureYear = 1900;

    // A fresh instance per call: EF Core tracks owned entities by reference, so one shared
    // instance attached to several vehicles would make them fight over the same owned entry.
    public static VehicleDetails Empty => new();

    // EF Core materialization and Empty only. Every other construction goes through Create.
    private VehicleDetails()
    {
    }

    /// <summary>The plate must not be printed on documents handed to third parties.</summary>
    public bool IsLicensePlateConfidential { get; private init; }

    public string? Make { get; private init; }

    /// <summary>Manufacturer's model designation ("Typbezeichnung"), for example <c>FH 16</c>.</summary>
    public string? Model { get; private init; }

    /// <summary>Month of manufacture; always the first day of that month.</summary>
    public DateOnly? ManufacturedOn { get; private init; }

    public int? PowerKw { get; private init; }

    public VehicleClass? VehicleClass { get; private init; }

    /// <summary>The subcontractor group ("Frächtergruppe") the vehicle is run under.</summary>
    public string? CarrierGroup { get; private init; }

    /// <summary>Vehicle identification number (VIN, "Fahrgestellnummer").</summary>
    public string? ChassisNumber { get; private init; }

    public string? EngineNumber { get; private init; }

    public int? LengthMm { get; private init; }

    public int? WidthMm { get; private init; }

    public int? HeightMm { get; private init; }

    public int? CurbWeightKg { get; private init; }

    /// <summary>Maximum permissible gross weight ("zulässiges Gesamtgewicht").</summary>
    public int? GrossWeightKg { get; private init; }

    /// <summary>Maximum permissible fifth-wheel load ("höchstzulässige Sattellast").</summary>
    public int? MaxFifthWheelLoadKg { get; private init; }

    public string? Notes { get; private init; }

    public static Result<VehicleDetails> Create(
        bool isLicensePlateConfidential = false,
        string? make = null,
        string? model = null,
        DateOnly? manufacturedOn = null,
        int? powerKw = null,
        VehicleClass? vehicleClass = null,
        string? carrierGroup = null,
        string? chassisNumber = null,
        string? engineNumber = null,
        int? lengthMm = null,
        int? widthMm = null,
        int? heightMm = null,
        int? curbWeightKg = null,
        int? grossWeightKg = null,
        int? maxFifthWheelLoadKg = null,
        string? notes = null)
    {
        VehicleDetails details = new()
        {
            IsLicensePlateConfidential = isLicensePlateConfidential,
            Make = OptionalText.Normalize(make),
            Model = OptionalText.Normalize(model),
            ManufacturedOn = manufacturedOn is { } date ? new DateOnly(date.Year, date.Month, 1) : null,
            PowerKw = powerKw,
            VehicleClass = vehicleClass,
            CarrierGroup = OptionalText.Normalize(carrierGroup),
            ChassisNumber = OptionalText.Normalize(chassisNumber)?.ToUpperInvariant(),
            EngineNumber = OptionalText.Normalize(engineNumber),
            LengthMm = lengthMm,
            WidthMm = widthMm,
            HeightMm = heightMm,
            CurbWeightKg = curbWeightKg,
            GrossWeightKg = grossWeightKg,
            MaxFifthWheelLoadKg = maxFifthWheelLoadKg,
            Notes = OptionalText.Normalize(notes),
        };

        Error? error = details.Validate();
        return error is null ? details : error;
    }

    private Error? Validate()
    {
        if (TooLong(Make, MaxNameLength) || TooLong(Model, MaxNameLength) || TooLong(CarrierGroup, MaxNameLength))
        {
            return Error.Validation(
                "Vehicle.TextTooLong", $"Make, model and carrier group must not exceed {MaxNameLength} characters.");
        }

        if (TooLong(ChassisNumber, MaxIdentifierLength) || TooLong(EngineNumber, MaxIdentifierLength))
        {
            return Error.Validation(
                "Vehicle.IdentifierTooLong",
                $"Chassis and engine number must not exceed {MaxIdentifierLength} characters.");
        }

        if (TooLong(Notes, MaxNotesLength))
        {
            return Error.Validation("Vehicle.NotesTooLong", $"Notes must not exceed {MaxNotesLength} characters.");
        }

        if (ManufacturedOn is { Year: < MinManufactureYear })
        {
            return Error.Validation(
                "Vehicle.ManufacturedOnTooEarly", $"The year of manufacture must not be before {MinManufactureYear}.");
        }

        if (VehicleClass is { } vehicleClass && !Enum.IsDefined(vehicleClass))
        {
            return Error.Validation("Vehicle.UnknownClass", $"'{vehicleClass}' is not a known vehicle class.");
        }

        if (OutOfRange(PowerKw, MaxPowerKw))
        {
            return Error.Validation("Vehicle.PowerOutOfRange", $"Power must be between 1 and {MaxPowerKw} kW.");
        }

        if (OutOfRange(LengthMm, MaxLengthMm) || OutOfRange(WidthMm, MaxWidthMm) || OutOfRange(HeightMm, MaxHeightMm))
        {
            return Error.Validation(
                "Vehicle.DimensionOutOfRange",
                $"Length must be between 1 and {MaxLengthMm} mm, width and height between 1 and {MaxWidthMm} mm.");
        }

        if (OutOfRange(CurbWeightKg, MaxWeightKg)
            || OutOfRange(GrossWeightKg, MaxWeightKg)
            || OutOfRange(MaxFifthWheelLoadKg, MaxWeightKg))
        {
            return Error.Validation(
                "Vehicle.WeightOutOfRange", $"Weights must be between 1 and {MaxWeightKg} kg.");
        }

        if (GrossWeightKg <= CurbWeightKg)
        {
            return Error.Validation(
                "Vehicle.GrossWeightNotAboveCurbWeight", "Gross weight must be greater than the curb weight.");
        }

        return null;
    }

    private static bool TooLong(string? value, int maxLength) => value is not null && value.Length > maxLength;

    private static bool OutOfRange(int? value, int max) => value is { } number && (number < 1 || number > max);
}
