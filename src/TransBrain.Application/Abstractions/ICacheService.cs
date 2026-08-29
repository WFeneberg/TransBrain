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
}
