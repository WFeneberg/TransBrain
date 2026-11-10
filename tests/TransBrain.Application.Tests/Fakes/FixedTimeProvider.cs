namespace TransBrain.Application.Tests.Fakes;

/// <summary>A clock frozen at one instant, so audit stamps can be asserted exactly.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 11, 9, 8, 30, 0, TimeSpan.Zero);

    public FixedTimeProvider()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
