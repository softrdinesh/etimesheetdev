using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// A business rule was violated. Maps to HTTP 400.
/// </summary>
public class BusinessException : AppException
{
    public BusinessException(string message, IReadOnlyCollection<string>? errors = null)
        : base(HttpStatusCode.BadRequest, message, errors)
    {
    }
}
