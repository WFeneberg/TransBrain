namespace TransBrain.Domain.Vehicles;

/// <summary>What kind of workshop visit a maintenance entry records ("Wartungsart").</summary>
public enum VehicleMaintenanceKind
{
    /// <summary>Scheduled service ("Wartung/Service"), for example an oil change.</summary>
    Service,

    /// <summary>Statutory inspection ("Prüfung"), for example the HU/TÜV or a tachograph check.</summary>
    Inspection,

    /// <summary>Unscheduled repair ("Reparatur").</summary>
    Repair,

    /// <summary>Tire change or tire service ("Reifenwechsel").</summary>
    TireChange,

    /// <summary>Anything else ("Sonstiges").</summary>
    Other
}
