using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// The caller is authenticated but is not allowed to perform the operation.
/// Maps to HTTP 403.
/// </summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You are not allowed to perform this operation.")
        : base(HttpStatusCode.Forbidden, message)
    {
    }
}
