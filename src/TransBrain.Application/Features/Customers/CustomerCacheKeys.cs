namespace TransBrain.Application.Features.Customers;

/// <summary>
/// The customer aggregate's cache-key prefix, shared by the list query and every write handler
/// so the two sides cannot silently drift apart.
/// </summary>
internal static class CustomerCacheKeys
{
    public const string Prefix = "customers:";
}
