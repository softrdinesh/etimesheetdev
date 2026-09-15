namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Abstraction over the process cache. Services depend on this rather than on
/// <c>IMemoryCache</c>, which keeps them unit-testable and leaves the door open
/// for a distributed cache without touching business code.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value, or default when absent or when caching is disabled.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cached value, or runs <paramref name="factory"/> and caches
    /// its result. Null results are not cached, so a miss is never made sticky.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts every entry whose key starts with <paramref name="keyPrefix"/>.
    /// This is how a write invalidates all cached reads for one user.
    /// </summary>
    Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);
}
