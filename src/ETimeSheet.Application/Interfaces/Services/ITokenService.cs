using ETimeSheet.Application.Models.Common;

namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Issues signed access tokens containing the user and role claims.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT carrying <c>ClaimConstants.UserId</c> and
    /// <c>ClaimConstants.RoleId</c>, valid for the configured lifetime.
    /// </summary>
    AccessToken CreateAccessToken(int userId, int roleId);
}
