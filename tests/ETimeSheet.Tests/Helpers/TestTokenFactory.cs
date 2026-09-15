using System.Net.Http.Headers;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Infrastructure.Services;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Tests.Helpers;

/// <summary>
/// Mints bearer tokens for integration tests using the production
/// <see cref="ITokenService"/>, so tests exercise the same claim layout and
/// signing algorithm the application issues.
/// <para>
/// Tokens are always issued against the real wall clock, never against the
/// frozen business clock. The JWT lifetime validator inside
/// <c>JwtBearerHandler</c> compares <c>exp</c> to <see cref="DateTime.UtcNow"/>
/// and cannot be substituted, so a token stamped with a frozen date would be
/// rejected as expired. The frozen clock governs business rules only.
/// </para>
/// </summary>
public static class TestTokenFactory
{
    public static AuthenticationHeaderValue BearerHeader(int userId, int roleId) =>
        new("Bearer", CreateToken(userId, roleId));

    public static string CreateToken(int userId, int roleId) =>
        BuildTokenService(DateTime.UtcNow).CreateAccessToken(userId, roleId).Value;

    /// <summary>
    /// Creates a token that has already expired, to prove the host really
    /// validates lifetime.
    /// </summary>
    public static string CreateExpiredToken(int userId, int roleId) =>
        BuildTokenService(DateTime.UtcNow.AddHours(-2), expiryMinutes: 1)
            .CreateAccessToken(userId, roleId)
            .Value;

    /// <summary>Creates an otherwise valid token signed with the wrong key.</summary>
    public static string CreateTokenWithWrongKey(int userId, int roleId) =>
        BuildTokenService(
                DateTime.UtcNow,
                secretKey: "a-completely-different-key-that-must-not-be-trusted")
            .CreateAccessToken(userId, roleId)
            .Value;

    /// <summary>Creates an otherwise valid token from an unexpected issuer.</summary>
    public static string CreateTokenWithWrongIssuer(int userId, int roleId) =>
        BuildTokenService(DateTime.UtcNow, issuer: "https://attacker.example")
            .CreateAccessToken(userId, roleId)
            .Value;

    private static ITokenService BuildTokenService(
        DateTime issuedAtUtc,
        int expiryMinutes = 60,
        string? secretKey = null,
        string? issuer = null,
        string? audience = null)
    {
        var settings = new JwtSettings
        {
            SecretKey = secretKey ?? ETimeSheetApiFactory.TestSigningKey,
            Issuer = issuer ?? ETimeSheetApiFactory.TestIssuer,
            Audience = audience ?? ETimeSheetApiFactory.TestAudience,
            ExpiryMinutes = expiryMinutes
        };

        return new TokenService(
            Options.Create(settings),
            new FixedDateTimeProvider(issuedAtUtc));
    }
}
