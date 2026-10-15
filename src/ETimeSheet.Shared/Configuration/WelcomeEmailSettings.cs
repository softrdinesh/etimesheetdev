using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// The welcome email sent when <c>create-timesheet-user</c> creates a new
/// account: its subject, and the values its template points the user to.
/// </summary>
public class WelcomeEmailSettings
{
    public const string SectionName = "WelcomeEmail";

    /// <summary>The email's subject line.</summary>
    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Where the user signs in - the template's <c>{{NexoraLoginURL}}</c>.</summary>
    [Required]
    [Url]
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>Who the user contacts for help - the template's <c>{{SupportEmail}}</c>.</summary>
    [Required]
    [EmailAddress]
    public string SupportEmail { get; set; } = string.Empty;
}
