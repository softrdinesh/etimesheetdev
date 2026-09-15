using System.Globalization;
using System.Security.Claims;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// Reads the caller's identity from the validated JWT claims.
/// <para>
/// This is the only type in the solution that touches
/// <c>HttpContext.User</c>. Everything else asks <see cref="ICurrentUserService"/>.
/// </para>
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId => ReadIntClaim(ClaimConstants.UserId);

    public int? RoleId => ReadIntClaim(ClaimConstants.RoleId);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public int GetRequiredUserId() =>
        UserId ?? throw new UnauthorizedException(
            "The request does not carry a valid user identity.");

    public int GetRequiredRoleId() =>
        RoleId ?? throw new UnauthorizedException(
            "The request does not carry a valid role identity.");

    /// <summary>
    /// Parses a claim as a positive integer. A missing, malformed or
    /// non-positive value is reported as "no identity" rather than being coerced
    /// to zero, so that a broken token can never pass as user 0.
    /// </summary>
    private int? ReadIntClaim(string claimType)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var rawValue = principal.FindFirstValue(claimType);

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
               && value > 0
            ? value
            : null;
    }
}
