using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// The external email service the <c>SendEmail</c> job posts each queued email
/// to, and how hard it tries.
/// </summary>
public class EmailServiceSettings
{
    public const string SectionName = "EmailService";

    /// <summary>
    /// The endpoint that sends one email - it takes
    /// <c>{ "emailaddress", "bodyContent", "subject" }</c>.
    /// </summary>
    [Required]
    [Url]
    public string SendEmailUrl { get; set; } = string.Empty;

    /// <summary>
    /// How many attempts an email gets. One that has failed this many times is
    /// left in the queue as <c>Error</c> and never tried again.
    /// </summary>
    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>How long one call to the email service may take, in seconds.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;
}
