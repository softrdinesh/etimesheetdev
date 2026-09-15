namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// The only approved way to find out who is calling. No other service or
/// controller may read <c>HttpContext.User</c> directly.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Authenticated user id, or null when unauthenticated or when the claim is missing/malformed.</summary>
    int? UserId { get; }

    /// <summary>Authenticated role id, or null when unauthenticated or when the claim is missing/malformed.</summary>
    int? RoleId { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Returns the caller's user id, or throws <see cref="Shared.Exceptions.UnauthorizedException"/>.
    /// Use this everywhere an operation needs an identity; it guarantees the
    /// value is a real user rather than a defaulted zero.
    /// </summary>
    int GetRequiredUserId();

    /// <summary>
    /// Returns the caller's role id, or throws <see cref="Shared.Exceptions.UnauthorizedException"/>.
    /// </summary>
    int GetRequiredRoleId();
}
