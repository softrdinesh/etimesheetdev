namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Uploads a document to the external document service and returns the
/// reference it was stored under.
/// <para>
/// Deliberately free of <c>IFormFile</c>: the caller hands over a stream, so
/// the implementation does not care whether the bytes came from a form.
/// </para>
/// </summary>
public interface IDocumentUploader
{
    /// <summary>
    /// Uploads <paramref name="content"/> under <paramref name="projectCode"/>.
    /// </summary>
    /// <returns>
    /// The reference the service returned for the stored file - what the
    /// caller saves so the file can be found again.
    /// </returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ExternalServiceException">
    /// The service could not be reached, did not answer in time, answered with
    /// an error, or answered without a usable reference.
    /// </exception>
    Task<string> UploadAsync(
        string projectCode,
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}
