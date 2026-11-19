using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// The requested resource does not exist. Maps to HTTP 404.
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(HttpStatusCode.NotFound, message)
    {
    }

    /// <summary>
    /// Builds a message, fit to show a user as it is, that names the kind of
    /// resource and nothing else about it.
    /// <para>
    /// <paramref name="key"/> is deliberately left out of the message: an id
    /// means nothing on screen. It stays on the signature so a call site still
    /// says which record it looked for.
    /// </para>
    /// </summary>
    public static NotFoundException For(string resourceName, object key) =>
        new($"The {resourceName.ToLowerInvariant()} was not found. It may have been removed - please refresh and try again.");
}
