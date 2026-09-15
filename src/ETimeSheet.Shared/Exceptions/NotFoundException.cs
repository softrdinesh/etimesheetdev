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
    /// Builds a message that names the resource without leaking anything else about it.
    /// </summary>
    public static NotFoundException For(string resourceName, object key) =>
        new($"{resourceName} '{key}' was not found.");
}
