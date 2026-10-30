using AwesomeAssertions;
using TransBrain.Domain.Routing;

namespace TransBrain.Domain.Tests.Routing;

public class DrivingTimePlannerTests
{
    // Monday, 06:00 UTC: 07:00 in Germany, inside a working day.
    private static readonly DateTimeOffset MondayMorning = new(2027, 3, 1, 6, 0, 0, TimeSpan.Zero);

    // Sunday, 06:00 UTC: 07:00 in Germany, inside the Sunday truck ban.
    private static readonly DateTimeOffset SundayMorning = new(2027, 3, 7, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Plan_LegLongerThanFourAndAHalfHours_InsertsFortyFiveMinuteBreak()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            MondayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(5))],
            appliesNationalTruckBans: true);

        ScheduleEntry breakEntry = schedule.Entries.Single(entry => entry.Kind == ScheduleEntryKind.Break);
        (breakEntry.End - breakEntry.Start).Should().Be(TimeSpan.FromMinutes(45));
        schedule.TotalDriving.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void Plan_DrivingBeyondTenHours_ReportsViolation()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            MondayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(4)), new PlannedLeg("DE", TimeSpan.FromHours(4)), new PlannedLeg("DE", TimeSpan.FromHours(2.5))],
            appliesNationalTruckBans: true);

        schedule.Violations.Should().ContainSingle(v =>
            v.Code == "Tour.DailyDrivingExceeded" && v.Severity == ViolationSeverity.Violation);
    }

    [Fact]
    public void Plan_DrivingBetweenNineAndTenHours_ReportsExtensionWarning()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            MondayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(9.5))],
            appliesNationalTruckBans: true);

        schedule.Violations.Should().ContainSingle(v =>
            v.Code == "Tour.ExtendedDailyDriving" && v.Severity == ViolationSeverity.Warning);
    }

    [Fact]
    public void Plan_DrivingOnSundayInGermany_ReportsNationalTruckBan()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            SundayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(1))],
            appliesNationalTruckBans: true);

        schedule.Violations.Should().ContainSingle(v =>
            v.Code == "Tour.NationalTruckBan" && v.CountryCode == "DE");
    }

    [Fact]
    public void Plan_DrivingOnSundayWhenBansDoNotApply_ReportsNoBan()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            SundayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(1))],
            appliesNationalTruckBans: false);

        schedule.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Plan_DrivingInCountryOutsideEu561_ReportsRegimeWarning()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            MondayMorning,
            [new PlannedLeg("TR", TimeSpan.FromHours(1))],
            appliesNationalTruckBans: true);

        schedule.Violations.Should().ContainSingle(v =>
            v.Code == "Tour.RegimeNotModelled" && v.CountryCode == "TR");
    }

    [Fact]
    public void Plan_AnyDay_EarliestNextStartIsElevenHoursAfterEnd()
    {
        DrivingSchedule schedule = DrivingTimePlanner.Plan(
            MondayMorning,
            [new PlannedLeg("DE", TimeSpan.FromHours(2))],
            appliesNationalTruckBans: true);

        schedule.EarliestNextStart.Should().Be(schedule.End + TimeSpan.FromHours(11));
    }
}
