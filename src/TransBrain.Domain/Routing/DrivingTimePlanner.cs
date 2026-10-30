using System.Globalization;

namespace TransBrain.Domain.Routing;

/// <summary>
/// Lays one working day on a timeline and inserts the breaks that EU 561/2006 requires.
/// </summary>
/// <remarks>
/// The day starts with a loading service at the first stop. Each leg is driven in one or more
/// chunks: a 45-minute break goes in whenever the continuous driving limit is reached and more
/// driving follows. Service time at a stop is other work, so it does not reset the continuous
/// counter; only a break does. The break is always a single 45-minute block, which is the
/// simpler of the two permitted forms (45 min, or 15 + 30). Weekly driving and rest limits
/// need history this single-day planner does not see, so they are not checked here.
/// </remarks>
public static class DrivingTimePlanner
{
    public static DrivingSchedule Plan(
        DateTimeOffset departure,
        IReadOnlyList<PlannedLeg> legs,
        bool appliesNationalTruckBans)
    {
        List<ScheduleEntry> entries = [];
        List<RuleViolation> violations = [];
        HashSet<string> reported = [];

        DateTimeOffset clock = departure;
        TimeSpan continuous = TimeSpan.Zero;
        TimeSpan daily = TimeSpan.Zero;

        void Service()
        {
            DateTimeOffset end = clock + DrivingRules.StopServiceTime;
            entries.Add(new ScheduleEntry(ScheduleEntryKind.Service, clock, end, null));
            clock = end;
        }

        void ReportOnce(RuleViolation violation)
        {
            if (reported.Add($"{violation.Code}|{violation.CountryCode}|{violation.Message}"))
            {
                violations.Add(violation);
            }
        }

        Service();

        foreach (PlannedLeg leg in legs)
        {
            if (!DrivingRules.FollowsEu561(leg.CountryCode))
            {
                ReportOnce(new RuleViolation(
                    ViolationSeverity.Warning,
                    "Tour.RegimeNotModelled",
                    $"Driving in {leg.CountryCode} follows rules this planner does not model. Check the rest times manually.",
                    leg.CountryCode));
            }

            TimeSpan remaining = leg.Driving;
            while (remaining > TimeSpan.Zero)
            {
                if (continuous >= DrivingRules.MaxContinuousDriving)
                {
                    entries.Add(new ScheduleEntry(
                        ScheduleEntryKind.Break,
                        clock,
                        clock + DrivingRules.RequiredBreak,
                        null));
                    clock += DrivingRules.RequiredBreak;
                    continuous = TimeSpan.Zero;
                }

                TimeSpan chunk = TimeSpan.FromTicks(Math.Min(
                    remaining.Ticks,
                    (DrivingRules.MaxContinuousDriving - continuous).Ticks));
                DateTimeOffset end = clock + chunk;

                if (appliesNationalTruckBans && IsInNationalBan(leg.CountryCode, clock, end))
                {
                    ReportOnce(new RuleViolation(
                        ViolationSeverity.Violation,
                        "Tour.NationalTruckBan",
                        $"Trucks may not drive in {leg.CountryCode} at this time: a Sunday truck driving ban applies.",
                        leg.CountryCode));
                }

                entries.Add(new ScheduleEntry(ScheduleEntryKind.Driving, clock, end, leg.CountryCode));
                clock = end;
                continuous += chunk;
                daily += chunk;
                remaining -= chunk;
            }

            Service();
        }

        string drivenToday = daily.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        if (daily > DrivingRules.MaxExtendedDailyDriving)
        {
            violations.Add(new RuleViolation(
                ViolationSeverity.Violation,
                "Tour.DailyDrivingExceeded",
                $"Planned driving of {drivenToday} exceeds the 10 h maximum for one day.",
                null));
        }
        else if (daily > DrivingRules.MaxDailyDriving)
        {
            violations.Add(new RuleViolation(
                ViolationSeverity.Warning,
                "Tour.ExtendedDailyDriving",
                $"Planned driving of {drivenToday} needs the 10 h extension, which is allowed twice a week.",
                null));
        }

        return new DrivingSchedule(
            entries,
            violations,
            clock,
            daily,
            clock + DrivingRules.RegularDailyRest);
    }

    private static bool IsInNationalBan(string countryCode, DateTimeOffset start, DateTimeOffset end)
    {
        if (!DrivingRules.TryGetNationalTruckBan(countryCode, out NationalTruckBan ban))
        {
            return false;
        }

        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(ban.TimeZoneId);
        DateTime localStart = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        DateTime localEnd = TimeZoneInfo.ConvertTime(end, zone).DateTime;

        for (DateTime day = localStart.Date; day <= localEnd.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek != ban.Day)
            {
                continue;
            }

            DateTime banStart = day + ban.From;
            DateTime banEnd = day + ban.To;
            if (localStart < banEnd && localEnd > banStart)
            {
                return true;
            }
        }

        return false;
    }
}
