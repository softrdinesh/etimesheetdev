using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// The one place that answers "may the current caller do this?".
/// <para>
/// Business code must never compare role ids inline. It asks for a permission
/// and lets this service decide, which means the role/permission matrix can
/// change - or move into the database - without touching any business rule.
/// </para>
/// </summary>
public interface IAuthorizationService
{
    /// <summary>Non-throwing check for the caller's role.</summary>
    bool IsInRole(RoleType role);

    /// <summary>Non-throwing check used when a rule branches on a permission rather than rejecting.</summary>
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="Shared.Exceptions.ForbiddenException"/> unless the caller holds the role.</summary>
    Task RequireRoleAsync(RoleType role, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="Shared.Exceptions.ForbiddenException"/> unless the caller holds at least one of the roles.</summary>
    Task RequireAnyRoleAsync(
        IReadOnlyCollection<RoleType> roles,
        CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="Shared.Exceptions.ForbiddenException"/> unless the caller holds the permission.</summary>
    Task RequirePermissionAsync(string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allows the operation when the caller owns the record, and otherwise falls
    /// back to the permission. This is the shape almost every per-user resource
    /// needs, so it is expressed once here instead of at each call site.
    /// </summary>
    Task RequireSelfOrPermissionAsync(
        int targetUserId,
        string permission,
        CancellationToken cancellationToken = default);
}
