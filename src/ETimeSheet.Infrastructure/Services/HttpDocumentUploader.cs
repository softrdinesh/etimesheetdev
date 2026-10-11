using System.Net.Http.Headers;
using System.Text.Json;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// <see cref="IDocumentUploader"/> over the external service's
/// <see cref="ExternalServiceEndpoints.Upload.UpdateDocument"/> endpoint. A
/// typed <see cref="HttpClient"/> whose <c>BaseAddress</c> is
/// <see cref="EmailServiceSettings.BaseUrl"/>, so handlers are pooled by
/// <c>IHttpClientFactory</c>.
/// </summary>
public class HttpDocumentUploader : IDocumentUploader
{
    /// <summary>
    /// The multipart part name the service reads the file from - per its
    /// published OpenAPI document.
    /// </summary>
    private const string FilePartName = "file";

    /// <summary>
    /// The service answers <c>{ "success", "message", "path" }</c>, in camel
    /// case; read case-insensitively so a change of casing on its side does not
    /// lose the path.
    /// </summary>
    private static readonly JsonSerializerOptions ResponseOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly EmailServiceSettings _settings;
    private readonly ILogger<HttpDocumentUploader> _logger;

    public HttpDocumentUploader(
        HttpClient httpClient,
        IOptions<EmailServiceSettings> settings,
        ILogger<HttpDocumentUploader> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        string projectCode,
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);

        if (!string.IsNullOrWhiteSpace(contentType)
            && MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            file.Headers.ContentType = mediaType;
        }

        form.Add(file, FilePartName, fileName);

        string body;

        try
        {
            using var response = await _httpClient.PostAsync(
                ExternalServiceEndpoints.Upload.UpdateDocumentFor(projectCode),
                form,
                cancellationToken);

            body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The body goes to the log, not to the caller: it is the
                // service's, and may describe its internals.
                _logger.LogWarning(
                    "Document upload for project {ProjectCode} failed: {StatusCode} {Reason}. Body: {Body}",
                    projectCode,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    body);

                throw new ExternalServiceException(
                    $"The document service refused the upload ({(int)response.StatusCode} {response.ReasonPhrase}).");
            }
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceException("The document service could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation; only a
            // cancellation the caller asked for is allowed to propagate.
            throw new ExternalServiceException(
                $"The document service did not answer within {_settings.TimeoutSeconds} seconds.",
                exception);
        }

        UploadResponse? answer;

        try
        {
            answer = JsonSerializer.Deserialize<UploadResponse>(body, ResponseOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                "Document upload for project {ProjectCode} answered with a body that is not JSON: {Body}",
                projectCode,
                body);

            throw new ExternalServiceException("The document service answered in a format this API does not understand.", exception);
        }

        // A 200 that says success: false, or that carries no path, is still a
        // failed upload - there is nothing to store.
        if (answer is not { Success: true } || string.IsNullOrWhiteSpace(answer.Path))
        {
            _logger.LogWarning(
                "Document upload for project {ProjectCode} did not succeed: success {Success}, message {Message}.",
                projectCode,
                answer?.Success,
                answer?.Message);

            throw new ExternalServiceException(
                "The document service did not accept the upload, or did not say where the file was stored.");
        }

        return answer.Path.Trim();
    }

    /// <summary>The service's answer to an upload.</summary>
    private sealed class UploadResponse
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        /// <summary>Where the file was stored - the value saved as the client's avatar.</summary>
        public string? Path { get; set; }
    }
}
