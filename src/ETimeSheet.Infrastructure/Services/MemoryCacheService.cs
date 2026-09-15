using System.Collections.Concurrent;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Shared.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// <see cref="IMemoryCache"/>-backed implementation of <see cref="ICacheService"/>.
/// <para>
/// <c>IMemoryCache</c> cannot enumerate its own keys, so this service keeps a
/// side index of the keys it created. That is what makes prefix invalidation -
/// "drop everything cached for user 42" - possible after a write.
/// </para>
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _settings;
    private readonly ILogger<MemoryCacheService> _logger;

    /// <summary>
    /// Keys currently believed to be in the cache. Entries remove themselves
    /// from here through an eviction callback, so the index cannot outgrow the cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.Ordinal);

    public MemoryCacheService(
        IMemoryCache memoryCache,
        IOptions<CacheSettings> settings,
        ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled)
        {
            return Task.FromResult<T?>(default);
        }

        return Task.FromResult(_memoryCache.TryGetValue(key, out T? value) ? value : default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled || value is null)
        {
            return Task.CompletedTask;
        }

        _memoryCache.Set(key, value, BuildEntryOptions(key, absoluteExpiration));
        _trackedKeys[key] = 0;

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled)
        {
            return await factory(cancellationToken);
        }

        if (_memoryCache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}.", key);
            return cached;
        }

        var value = await factory(cancellationToken);

        // A null result is not cached: caching "nothing" would turn a transient
        // miss into a sticky one for the whole expiry window.
        if (value is not null)
        {
            _memoryCache.Set(key, value, BuildEntryOptions(key, absoluteExpiration));
            _trackedKeys[key] = 0;
        }

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _memoryCache.Remove(key);
        _trackedKeys.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matches = _trackedKeys.Keys
            .Where(key => key.StartsWith(keyPrefix, StringComparison.Ordinal))
            .ToArray();

        foreach (var key in matches)
        {
            _memoryCache.Remove(key);
            _trackedKeys.TryRemove(key, out _);
        }

        if (matches.Length > 0)
        {
            _logger.LogDebug(
                "Evicted {EvictedCount} cache entries for prefix {CachePrefix}.",
                matches.Length,
                keyPrefix);
        }

        return Task.CompletedTask;
    }

    private MemoryCacheEntryOptions BuildEntryOptions(string key, TimeSpan? absoluteExpiration)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? _settings.DefaultExpiration,
            // Every entry counts as one unit against CacheSettings.SizeLimit.
            Size = 1
        };

        options.RegisterPostEvictionCallback(
            (evictedKey, _, _, _) => _trackedKeys.TryRemove((string)evictedKey, out _));

        return options;
    }
}
