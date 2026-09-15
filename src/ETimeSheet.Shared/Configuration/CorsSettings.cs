namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// Cross-origin settings. Origins come from configuration only; there is no
/// code path that allows any origin.
/// </summary>
public class CorsSettings
{
    public const string SectionName = "Cors";

    public const string PolicyName = "ETimeSheetCorsPolicy";

    /// <summary>Exact origins permitted to call the API, e.g. "https://app.example.com".</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>Required when the browser client sends cookies or uses credentialed requests.</summary>
    public bool AllowCredentials { get; set; } = true;
}
