namespace TransBrain.Application.Features.Vehicles;

/// <summary>
/// The vehicle aggregate's cache-key prefix, shared by the list query (to build its keys and to
/// read the current invalidation generation) and every write handler (to invalidate). One
/// constant so the two sides cannot silently drift apart.
/// </summary>
internal static class VehicleCacheKeys
{
    public const string Prefix = "vehicles:";
}
