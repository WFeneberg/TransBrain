namespace TransBrain.Domain.Routing;

public enum ScheduleEntryKind
{
    Driving,
    Break,
    Service
}

public enum ViolationSeverity
{
    Warning,
    Violation
}

/// <summary>One driver activity on the timeline.</summary>
public sealed record ScheduleEntry(
    ScheduleEntryKind Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? CountryCode);

/// <summary>A rule the planned day breaks, or a situation the planner cannot judge.</summary>
public sealed record RuleViolation(
    ViolationSeverity Severity,
    string Code,
    string Message,
    string? CountryCode);

/// <summary>Driving leg as the planner sees it: the country it is driven in and its duration.</summary>
public sealed record PlannedLeg(string CountryCode, TimeSpan Driving);

public sealed record DrivingSchedule(
    IReadOnlyList<ScheduleEntry> Entries,
    IReadOnlyList<RuleViolation> Violations,
    DateTimeOffset End,
    TimeSpan TotalDriving,
    DateTimeOffset EarliestNextStart);
