using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Sends one email through the external email service. Keeps HTTP out of the
/// Application layer: the service decides what to send, this decides how.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>. A failure - the service unreachable,
    /// timing out, or answering with an error status - is <b>returned</b>, not
    /// thrown, so the caller can record it against the queued email and move
    /// on to the next. Only cancellation throws.
    /// </summary>
    Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
