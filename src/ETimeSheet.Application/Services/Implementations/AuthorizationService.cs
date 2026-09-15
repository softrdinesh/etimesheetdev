using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Role and permission decisions for the current caller.
/// <para>
/// The matrix below is the only place in the solution that knows which role
/// grants which permission. When the final matrix is agreed it can be replaced
/// with a database-backed lookup by changing <see cref="GetPermissions"/> alone -
/// no business rule references a role id.
/// </para>
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private static readonly IReadOnlyDictionary<RoleType, IReadOnlySet<string>> RolePermissions =
        new Dictionary<RoleType, IReadOnlySet<string>>
        {
            // An employee sees only their own entries, so they hold nothing.
            [RoleType.Employee] = new HashSet<string>(StringComparer.Ordinal),

            [RoleType.Manager] = new HashSet<string>(StringComparer.Ordinal)
            {
                Permissions.TimeLogs.ViewAll
            },

            [RoleType.Administrator] = new HashSet<string>(StringComparer.Ordinal)
            {
                Permissions.TimeLogs.ViewAll
            }
        };

    private static readonly IReadOnlySet<string> NoPermissions =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(
        ICurrentUserService currentUserService,
        ILogger<AuthorizationService> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public bool IsInRole(RoleType role) => _currentUserService.RoleId == (int)role;

    public Task<bool> HasPermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetPermissions().Contains(permission));
    }

    public Task RequireRoleAsync(RoleType role, CancellationToken cancellationToken = default) =>
        RequireAnyRoleAsync(new[] { role }, cancellationToken);

    public Task RequireAnyRoleAsync(
        IReadOnlyCollection<RoleType> roles,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Reading the role through GetRequiredRoleId means an unauthenticated or
        // claim-less caller gets 401, while an authenticated caller with the
        // wrong role gets 403. Those are genuinely different answers.
        var roleId = _currentUserService.GetRequiredRoleId();

        if (roles.Any(role => (int)role == roleId))
        {
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Authorization denied for user {UserId} in role {RoleId}: required one of {RequiredRoles}.",
            _currentUserService.UserId,
            roleId,
            roles);

        throw new ForbiddenException();
    }

    public Task RequirePermissionAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roleId = _currentUserService.GetRequiredRoleId();

        if (GetPermissions().Contains(permission))
        {
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Authorization denied for user {UserId} in role {RoleId}: missing permission {Permission}.",
            _currentUserService.UserId,
            roleId,
            permission);

        throw new ForbiddenException();
    }

    public Task RequireSelfOrPermissionAsync(
        int targetUserId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_currentUserService.GetRequiredUserId() == targetUserId)
        {
            return Task.CompletedTask;
        }

        return RequirePermissionAsync(permission, cancellationToken);
    }

    /// <summary>
    /// Resolves the caller's permission set. Replace the backing store here when
    /// the role matrix moves to the database.
    /// </summary>
    private IReadOnlySet<string> GetPermissions()
    {
        var roleId = _currentUserService.RoleId;

        if (roleId is null || !Enum.IsDefined(typeof(RoleType), roleId.Value))
        {
            return NoPermissions;
        }

        return RolePermissions.TryGetValue((RoleType)roleId.Value, out var permissions)
            ? permissions
            : NoPermissions;
    }
}
