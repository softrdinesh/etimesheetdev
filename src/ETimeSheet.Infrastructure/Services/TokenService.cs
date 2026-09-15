using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Common;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Shared.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// Issues signed JWTs. The secret and lifetime come from configuration; nothing
/// here is hard-coded, and neither the secret nor the produced token is logged.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TokenService(IOptions<JwtSettings> jwtSettings, IDateTimeProvider dateTimeProvider)
    {
        _jwtSettings = jwtSettings.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public AccessToken CreateAccessToken(int userId, int roleId)
    {
        var issuedAt = _dateTimeProvider.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_jwtSettings.ExpiryMinutes);

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        var claims = new List<Claim>
        {
            // jti gives each token a unique identity, which is what a future
            // revocation list would key on.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimConstants.UserId, userId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimConstants.RoleId, roleId.ToString(CultureInfo.InvariantCulture))
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
