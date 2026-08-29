namespace TransBrain.Application.Features.Drivers;

/// <summary>
/// The driver aggregate's cache-key prefix, shared by the list query (to build its keys and to
/// read the current invalidation generation) and every write handler (to invalidate). One
/// constant so the two sides cannot silently drift apart.
/// </summary>
internal static class DriverCacheKeys
{
    public const string Prefix = "drivers:";
}
