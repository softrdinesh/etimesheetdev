using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// Base type for every exception the application throws deliberately.
/// <para>
/// Carrying the HTTP status code on the exception keeps the exception-handling
/// middleware free of a growing <c>switch</c> over exception types, and keeps
/// the decision about "what does this mean to the caller" next to the error.
/// </para>
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(
        HttpStatusCode statusCode,
        string message,
        IReadOnlyCollection<string>? errors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Errors = errors ?? Array.Empty<string>();
    }

    /// <summary>HTTP status code that represents this failure to the caller.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Optional caller-facing detail lines. Never contains internal diagnostics.</summary>
    public IReadOnlyCollection<string> Errors { get; }
}
