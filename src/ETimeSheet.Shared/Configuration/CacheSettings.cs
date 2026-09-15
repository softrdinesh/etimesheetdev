using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// In-memory cache tuning. Expiry lives in configuration so it can be shortened
/// in production without a code change.
/// </summary>
public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>Master switch. When false, <c>ICacheService</c> always misses.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Absolute expiry applied when a caller does not specify one.</summary>
    [Range(1, 3600)]
    public int DefaultExpirationSeconds { get; set; } = 120;

    /// <summary>Upper bound on entries, so a large tenant cannot exhaust memory.</summary>
    [Range(16, 1_000_000)]
    public long SizeLimit { get; set; } = 2048;

    public TimeSpan DefaultExpiration => TimeSpan.FromSeconds(DefaultExpirationSeconds);
}
