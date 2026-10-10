using System.Net.Http.Json;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// <see cref="IEmailSender"/> over the external email service's
/// <c>SendEmail</c> endpoint - <see cref="EmailServiceSettings.SendEmailUrl"/>.
/// A typed <see cref="HttpClient"/>, so its handlers are pooled by
/// <c>IHttpClientFactory</c> rather than created per send.
/// </summary>
public class HttpEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailServiceSettings _settings;

    public HttpEmailSender(HttpClient httpClient, IOptions<EmailServiceSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        // The service's payload, spelled exactly as it expects.
        var payload = new
        {
            emailaddress = message.EmailAddress,
            bodyContent = message.BodyContent,
            subject = message.Subject
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_settings.SendEmailUrl, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return EmailSendResult.Success;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return EmailSendResult.Failed(
                $"Email service answered {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
        catch (HttpRequestException exception)
        {
            return EmailSendResult.Failed($"Email service could not be reached: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation; only a
            // cancellation the caller asked for is allowed to propagate.
            return EmailSendResult.Failed(
                $"Email service did not answer within {_settings.TimeoutSeconds} seconds.");
        }
    }
}
