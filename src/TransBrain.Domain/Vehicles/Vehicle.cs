using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

public sealed class Vehicle
{
    private Vehicle()
    {
        LicensePlate = null!;
    }

    private Vehicle(
        Guid id,
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue,
        VehicleStatus status)
    {
        Id = id;
        LicensePlate = licensePlate;
        Type = type;
        PayloadKg = payloadKg;
        LoadMeters = loadMeters;
        NextInspectionDue = nextInspectionDue;
        Status = status;
    }

    public Guid Id { get; private set; }

    public LicensePlate LicensePlate { get; private set; }

    public VehicleType Type { get; private set; }

    public int PayloadKg { get; private set; }

    public decimal LoadMeters { get; private set; }

    public DateOnly NextInspectionDue { get; private set; }

    public VehicleStatus Status { get; private set; }

    public static Result<Vehicle> Create(
        LicensePlate licensePlate,
        VehicleType type,
        int payloadKg,
        decimal loadMeters,
        DateOnly nextInspectionDue)
    {
        if (payloadKg <= 0)
        {
            return Error.Validation("Vehicle.PayloadKgNotPositive", "Payload must be greater than zero.");
        }

        if (loadMeters <= 0m)
        {
            return Error.Validation("Vehicle.LoadMetersNotPositive", "Load meters must be greater than zero.");
        }

        return new Vehicle(
            Guid.CreateVersion7(),
            licensePlate,
            type,
            payloadKg,
            loadMeters,
            nextInspectionDue,
            VehicleStatus.Available);
    }
}
