using System.Security.Claims;
using ETimeSheet.Infrastructure.Services;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ETimeSheet.Tests.Unit.Services;

/// <summary>
/// Tests for claim reading. The important behaviour here is what happens when
/// claims are absent or malformed: the answer must be "no identity", never a
/// defaulted user id.
/// </summary>
[Trait("Category", "Unit")]
public class CurrentUserServiceTests
{
    [Fact]
    public void ReadsBothClaimsFromAnAuthenticatedPrincipal()
    {
        var service = BuildService(userId: "1001", roleId: "2");

        Assert.True(service.IsAuthenticated);
        Assert.Equal(1001, service.UserId);
        Assert.Equal(2, service.RoleId);
        Assert.Equal(1001, service.GetRequiredUserId());
        Assert.Equal(2, service.GetRequiredRoleId());
    }

    [Fact]
    public void WithNoHttpContext_ReportsNoIdentity()
    {
        var service = new CurrentUserService(new HttpContextAccessor());

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Throws<UnauthorizedException>(() => service.GetRequiredUserId());
    }

    [Fact]
    public void WithAnUnauthenticatedPrincipal_IgnoresAnyClaimsPresent()
    {
        // An unauthenticated identity can still carry claims; they must not count.
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimConstants.UserId, "1001"),
            new Claim(ClaimConstants.RoleId, "2")
        });

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Null(service.RoleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2147483648")]
    public void WithAMalformedUserClaim_ReportsNoIdentityRatherThanZero(string rawUserId)
    {
        var service = BuildService(userId: rawUserId, roleId: "2");

        // Coercing a broken claim to 0 would let a malformed token act as "user 0".
        Assert.Null(service.UserId);
        Assert.Throws<UnauthorizedException>(() => service.GetRequiredUserId());
    }

    [Fact]
    public void WithAMissingRoleClaim_GetRequiredRoleIdThrowsUnauthorized()
    {
        var service = BuildService(userId: "1001", roleId: null);

        Assert.Equal(1001, service.UserId);
        Assert.Null(service.RoleId);
        Assert.Throws<UnauthorizedException>(() => service.GetRequiredRoleId());
    }

    private static CurrentUserService BuildService(string? userId, string? roleId)
    {
        var claims = new List<Claim>();

        if (userId is not null)
        {
            claims.Add(new Claim(ClaimConstants.UserId, userId));
        }

        if (roleId is not null)
        {
            claims.Add(new Claim(ClaimConstants.RoleId, roleId));
        }

        // A non-empty authentication type is what makes the identity authenticated.
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new CurrentUserService(new HttpContextAccessor { HttpContext = context });
    }
}
