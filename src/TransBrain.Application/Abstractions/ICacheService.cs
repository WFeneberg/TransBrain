namespace TransBrain.Application.Abstractions;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Drops every entry whose key starts with <paramref name="prefix"/>. Write handlers call
    /// this with the aggregate's prefix, because a single write can invalidate every page and
    /// every filter combination, not merely the page it touched.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>
    /// Current generation counter for <paramref name="prefix"/> (0 if never invalidated). A list
    /// query handler folds this value into every cache key it builds, reading it once before it
    /// touches the database and its own <see cref="SetAsync{T}"/> call. If a write commits and
    /// calls <see cref="RemoveByPrefixAsync"/> in that window, the generation bumps and the
    /// reader's eventual <c>SetAsync</c> lands under a generation nothing will ever look up
    /// again - the stale value becomes unreachable instead of surviving as the authoritative
    /// answer for a fresh request. This closes the read-then-set race that plain key deletion
    /// cannot: deleting entries only removes what already exists at invalidation time, it cannot
    /// stop an in-flight read from writing something stale a moment later.
    /// </summary>
    Task<long> GetGenerationAsync(string prefix, CancellationToken cancellationToken);
}
