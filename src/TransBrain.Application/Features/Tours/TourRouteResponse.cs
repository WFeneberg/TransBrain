using TransBrain.Domain.Routing;

namespace TransBrain.Application.Features.Tours;

public sealed record RouteStopResponse(
    int Sequence,
    string OrderNumber,
    string StopType,
    string CountryCode,
    string Place,
    double Latitude,
    double Longitude);

public sealed record RoutePointResponse(double Latitude, double Longitude);

public sealed record ScheduleEntryResponse(
    string Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? CountryCode);

public sealed record RuleViolationResponse(string Severity, string Code, string Message, string? CountryCode);

/// <summary>
/// A tour's planned road route and the driver's timeline over it. Distances and durations come
/// from the truck routing service; the timeline and its rule checks come from the driving-time
/// planner. Warnings and violations are returned together, so a dispatcher sees every issue.
/// </summary>
public sealed record TourRouteResponse(
    Guid TourId,
    DateTimeOffset Departure,
    DateTimeOffset End,
    DateTimeOffset EarliestNextStart,
    double DistanceMeters,
    double DrivingSeconds,
    IReadOnlyList<RouteStopResponse> Stops,
    IReadOnlyList<RoutePointResponse> Geometry,
    IReadOnlyList<ScheduleEntryResponse> Schedule,
    IReadOnlyList<RuleViolationResponse> Violations)
{
    public static TourRouteResponse From(
        Guid tourId,
        DateTimeOffset departure,
        DrivingSchedule schedule,
        IReadOnlyList<RouteStopResponse> stops,
        IReadOnlyList<RoutePointResponse> geometry,
        double distanceMeters) =>
        new(
            tourId,
            departure,
            schedule.End,
            schedule.EarliestNextStart,
            distanceMeters,
            schedule.TotalDriving.TotalSeconds,
            stops,
            geometry,
            schedule.Entries
                .Select(entry => new ScheduleEntryResponse(
                    entry.Kind.ToString(),
                    entry.Start,
                    entry.End,
                    entry.CountryCode))
                .ToArray(),
            schedule.Violations
                .Select(violation => new RuleViolationResponse(
                    violation.Severity.ToString(),
                    violation.Code,
                    violation.Message,
                    violation.CountryCode))
                .ToArray());
}
