using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Services.Implementations;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ETimeSheet.Tests.Unit.Services;

/// <summary>
/// Tests for the central role/permission decisions. These lock down the matrix
/// so a future change to it cannot silently widen someone's access.
/// </summary>
[Trait("Category", "Unit")]
public class AuthorizationServiceTests
{
    private const int UserId = 1001;

    [Theory]
    [InlineData(RoleType.Employee, Permissions.TimeLogs.ViewAll, false)]
    [InlineData(RoleType.Manager, Permissions.TimeLogs.ViewAll, true)]
    [InlineData(RoleType.Administrator, Permissions.TimeLogs.ViewAll, true)]
    public async Task HasPermissionAsync_FollowsTheRoleMatrix(
        RoleType role,
        string permission,
        bool expected)
    {
        var service = BuildService(role);

        Assert.Equal(expected, await service.HasPermissionAsync(permission));
    }

    [Fact]
    public async Task HasPermissionAsync_ForAnUnknownPermission_GrantsNothing()
    {
        var service = BuildService(RoleType.Administrator);

        // Even the most privileged role must fail closed on a permission that
        // is not in the matrix, rather than being treated as "allow everything".
        Assert.False(await service.HasPermissionAsync("timelogs.not.a.real.permission"));
    }

    [Fact]
    public async Task RequirePermissionAsync_WhenGranted_DoesNotThrow()
    {
        var service = BuildService(RoleType.Manager);

        await service.RequirePermissionAsync(Permissions.TimeLogs.ViewAll);
    }

    [Fact]
    public async Task RequirePermissionAsync_WhenMissing_ThrowsForbidden()
    {
        var service = BuildService(RoleType.Employee);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RequirePermissionAsync(Permissions.TimeLogs.ViewAll));
    }

    [Fact]
    public async Task RequireRoleAsync_WhenTheRoleMatches_DoesNotThrow()
    {
        var service = BuildService(RoleType.Administrator);

        await service.RequireRoleAsync(RoleType.Administrator);
    }

    [Fact]
    public async Task RequireRoleAsync_WhenTheRoleDiffers_ThrowsForbidden()
    {
        var service = BuildService(RoleType.Employee);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RequireRoleAsync(RoleType.Manager));
    }

    [Fact]
    public async Task RequireAnyRoleAsync_AcceptsAnyListedRole()
    {
        var service = BuildService(RoleType.Manager);

        await service.RequireAnyRoleAsync(new[] { RoleType.Manager, RoleType.Administrator });
    }

    [Fact]
    public async Task RequireSelfOrPermissionAsync_ForYourOwnRecord_NeedsNoPermission()
    {
        var service = BuildService(RoleType.Employee);

        // An employee holds no permissions at all, yet may still act on their own data.
        await service.RequireSelfOrPermissionAsync(UserId, Permissions.TimeLogs.ViewAll);
    }

    [Fact]
    public async Task RequireSelfOrPermissionAsync_ForSomeoneElse_FallsBackToThePermission()
    {
        var service = BuildService(RoleType.Employee);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RequireSelfOrPermissionAsync(9999, Permissions.TimeLogs.ViewAll));
    }

    [Fact]
    public async Task RequireSelfOrPermissionAsync_ForSomeoneElse_SucceedsWithThePermission()
    {
        var service = BuildService(RoleType.Administrator);

        await service.RequireSelfOrPermissionAsync(9999, Permissions.TimeLogs.ViewAll);
    }

    [Fact]
    public async Task RequirePermissionAsync_WhenUnauthenticated_ThrowsUnauthorizedNotForbidden()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.RoleId).Returns((int?)null);
        currentUser
            .Setup(service => service.GetRequiredRoleId())
            .Throws(new UnauthorizedException());

        var service = new AuthorizationService(
            currentUser.Object,
            NullLogger<AuthorizationService>.Instance);

        // "Who are you?" and "you may not" are different answers, and clients
        // need to tell them apart to know whether to re-authenticate.
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.RequirePermissionAsync(Permissions.TimeLogs.ViewAll));
    }

    [Fact]
    public async Task HasPermissionAsync_ForAnUnknownRoleId_GrantsNothing()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.RoleId).Returns(9_999);
        currentUser.Setup(service => service.GetRequiredRoleId()).Returns(9_999);

        var service = new AuthorizationService(
            currentUser.Object,
            NullLogger<AuthorizationService>.Instance);

        // A role id the application does not recognise must fail closed.
        Assert.False(await service.HasPermissionAsync(Permissions.TimeLogs.ViewAll));
    }

    [Fact]
    public void IsInRole_ComparesAgainstTheCallersRoleId()
    {
        var service = BuildService(RoleType.Manager);

        Assert.True(service.IsInRole(RoleType.Manager));
        Assert.False(service.IsInRole(RoleType.Administrator));
    }

    private static AuthorizationService BuildService(RoleType role)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(UserId);
        currentUser.SetupGet(service => service.RoleId).Returns((int)role);
        currentUser.Setup(service => service.GetRequiredUserId()).Returns(UserId);
        currentUser.Setup(service => service.GetRequiredRoleId()).Returns((int)role);

        return new AuthorizationService(
            currentUser.Object,
            NullLogger<AuthorizationService>.Instance);
    }
}
