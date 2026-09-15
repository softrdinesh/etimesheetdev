using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ETimeSheet.Infrastructure.Data.Interceptors;

/// <summary>
/// Stamps audit columns on every insert and update.
/// <para>
/// Doing this in one interceptor means no repository or service can forget it,
/// and audit data cannot be forged by a request payload.
/// </para>
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInformation(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _dateTimeProvider.UtcNow;

        // A null user id is expected for background work; it is never a silent
        // fallback for a real caller, because every request path resolves the
        // identity through GetRequiredUserId() before it reaches this point.
        var userId = _currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreateDate = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateDate = now;
                    entry.Entity.UpdatedBy = userId;
                    ProtectCreationColumns(entry);
                    break;
            }
        }
    }

    /// <summary>
    /// Creation columns are write-once. Without this, a detached-then-attached
    /// entity would happily overwrite them with defaults.
    /// </summary>
    private static void ProtectCreationColumns(EntityEntry<AuditableEntity> entry)
    {
        entry.Property(entity => entity.CreateDate).IsModified = false;
        entry.Property(entity => entity.CreatedBy).IsModified = false;
    }
}
