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

        // A null user id is expected for background work, and is currently the
        // only possible answer: authentication is switched off for this project,
        // so nothing ever populates the principal.
        //
        // That is why the assignments below fall back to whatever the caller
        // already put on the entity rather than overwriting it. The security
        // property is unchanged - an AUTHENTICATED identity still wins, so a
        // payload cannot forge the author once JWT is back on - but while there
        // is no identity at all, a service that knows who is acting can say so
        // instead of every row recording a null author. The fallback becomes
        // dead code the day the principal is populated again.
        var userId = _currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreateDate = now;
                    entry.Entity.CreatedBy = userId ?? entry.Entity.CreatedBy;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdateDate = now;
                    entry.Entity.UpdatedBy = userId ?? entry.Entity.UpdatedBy;
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
