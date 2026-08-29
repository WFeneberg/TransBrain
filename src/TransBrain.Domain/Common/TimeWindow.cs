namespace TransBrain.Domain.Common;

public sealed record TimeWindow
{
    private TimeWindow(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    public static Result<TimeWindow> Create(DateTimeOffset from, DateTimeOffset to)
    {
        // Normalise before comparing, so a window sent in local time compares the same way
        // as the identical window sent in UTC.
        DateTimeOffset utcFrom = from.ToUniversalTime();
        DateTimeOffset utcTo = to.ToUniversalTime();

        if (utcFrom >= utcTo)
        {
            return Error.Validation("TimeWindow.FromNotBeforeTo", "The window's start must be before its end.");
        }

        return new TimeWindow(utcFrom, utcTo);
    }
}
