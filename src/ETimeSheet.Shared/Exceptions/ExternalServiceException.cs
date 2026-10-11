using System.Net;

namespace ETimeSheet.Shared.Exceptions;

/// <summary>
/// An external service this API depends on failed, or answered with something
/// unusable. Mapped to <b>502 Bad Gateway</b>: the request itself was fine, and
/// retrying it later may succeed.
/// <para>
/// The message is shown to the caller, so it names the service and what went
/// wrong - never the service's response body, which may contain internals.
/// </para>
/// </summary>
public class ExternalServiceException : AppException
{
    public ExternalServiceException(string message, Exception? innerException = null)
        : base(HttpStatusCode.BadGateway, message, null, innerException)
    {
    }
}
