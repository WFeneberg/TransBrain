namespace TransBrain.Domain.Routing;

/// <summary>
/// The driving and rest rules the planner applies, per country.
/// </summary>
/// <remarks>
/// Driving and rest times for road transport are harmonised by Regulation (EC) No 561/2006 in
/// the EU, the EEA, Switzerland (by agreement) and Great Britain (retained law). Those countries
/// share one rule set here. Countries outside that group follow AETR or national rules that
/// this planner does not model, so a tour that enters one is reported, not silently planned.
/// National traffic bans are a separate layer: they apply on top of the driving-time rules and
/// are listed per country in <see cref="NationalTruckBans"/>.
/// Holidays are not modelled, so a holiday ban is not detected.
/// Verify these values against current law before relying on them for compliance.
/// </remarks>
public static class DrivingRules
{
    /// <summary>Dispatch day starts and ends in this zone; used for the default departure time.</summary>
    public const string DispatchTimeZoneId = "Europe/Berlin";

    public static readonly TimeSpan MaxContinuousDriving = TimeSpan.FromHours(4.5);

    public static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(45);

    public static readonly TimeSpan MaxDailyDriving = TimeSpan.FromHours(9);

    /// <summary>Allowed twice a week; the planner warns rather than refuses, because it cannot see the week.</summary>
    public static readonly TimeSpan MaxExtendedDailyDriving = TimeSpan.FromHours(10);

    public static readonly TimeSpan RegularDailyRest = TimeSpan.FromHours(11);

    /// <summary>Time spent loading or unloading at each stop. This is other work, not a break.</summary>
    public static readonly TimeSpan StopServiceTime = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> Eu561Countries =
    [
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE", "IT",
        "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
        "IS", "LI", "NO", "CH", "GB"
    ];

    private static readonly Dictionary<string, NationalTruckBan> Bans = new()
    {
        ["DE"] = new NationalTruckBan("Europe/Berlin", DayOfWeek.Sunday, TimeSpan.Zero, TimeSpan.FromDays(1)),
        ["AT"] = new NationalTruckBan("Europe/Vienna", DayOfWeek.Sunday, TimeSpan.Zero, TimeSpan.FromHours(22))
    };

    public static bool FollowsEu561(string countryCode) => Eu561Countries.Contains(countryCode);

    public static bool TryGetNationalTruckBan(string countryCode, out NationalTruckBan ban) =>
        Bans.TryGetValue(countryCode, out ban!);
}

/// <summary>A weekly window in local time during which trucks may not drive in one country.</summary>
public sealed record NationalTruckBan(string TimeZoneId, DayOfWeek Day, TimeSpan From, TimeSpan To);
