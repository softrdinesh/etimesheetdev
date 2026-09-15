using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// Bearer token settings. Bound with the options pattern and validated at
/// startup, so a deployment with a missing or weak secret fails immediately
/// instead of issuing unverifiable tokens.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC signing key. Supply it from an environment variable, user secrets or
    /// a secret store - never from a checked-in appsettings file.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Jwt:SecretKey must be at least 32 characters for HMAC-SHA256.")]
    public string SecretKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Tolerance for clock drift between the issuer and this API. Defaults to
    /// zero rather than the framework default of five minutes, so expiry means expiry.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; }
}
