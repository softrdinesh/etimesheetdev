using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// The caller is not authenticated, or the required identity claims are missing
/// or malformed. Maps to HTTP 401.
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication is required.")
        : base(HttpStatusCode.Unauthorized, message)
    {
    }
}
