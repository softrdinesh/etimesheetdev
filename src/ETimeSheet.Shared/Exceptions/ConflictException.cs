using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// The operation conflicts with the current state of the resource. Maps to HTTP 409.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message, IReadOnlyCollection<string>? errors = null)
        : base(HttpStatusCode.Conflict, message, errors)
    {
    }
}
