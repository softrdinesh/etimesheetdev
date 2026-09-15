using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// SQL Server connection and resilience settings.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Number of retries for transient SQL failures.</summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Logs parameter values in EF Core diagnostics. Never enable outside local
    /// development - it writes user data into the logs.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; }
}
