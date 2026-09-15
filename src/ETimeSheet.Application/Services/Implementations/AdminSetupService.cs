using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.DTOs.AdminSetups;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the AdminSetup module.
/// <para>
/// It owns the four rules that matter here: whether a save is an insert or an
/// update, that a user may hold only one setup, what a soft delete actually
/// means, and who gets stamped into the audit columns. It reaches the database
/// only through <see cref="IAdminSetupRepository"/> and never sees
/// <c>Context</c>.
/// </para>
/// <para>
/// <b>No authorisation check.</b> Administrative CRUD is exactly the surface
/// that should be behind a permission, and it is not, because authentication is
/// switched off for this project for now. The acting user therefore comes from
/// the payload. Both of those change together when JWT is turned back on.
/// </para>
/// </summary>
public class AdminSetupService : IAdminSetupService
{
    private readonly IAdminSetupRepository _adminSetupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AdminSetupService> _logger;

    public AdminSetupService(
        IAdminSetupRepository adminSetupRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<AdminSetupService> logger)
    {
        _adminSetupRepository = adminSetupRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<AdminSetupResponse> SaveAsync(
        AdminSetupSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // The whole point of the shared endpoint: the client sends the same
        // payload either way and does not have to know which operation it is
        // performing. An id that is present and positive means "edit that row";
        // anything else means "add". The validator has already rejected a
        // present-but-non-positive id, so this cannot silently insert after a
        // client sent a broken id.
        return request.SetupId is > 0
            ? await UpdateExistingAsync(request.SetupId.Value, request, cancellationToken)
            : await AddNewAsync(request, cancellationToken);
    }

    public async Task<AdminSetupResponse> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminSetupRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw NotFoundException.For("Timesheet setup for user", userId);

        return setup.ToResponse();
    }

    public async Task DeleteAsync(
        AdminSetupDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminSetupRepository.GetForUpdateAsync(request.SetupId, cancellationToken)
            ?? throw NotFoundException.For("Timesheet setup", request.SetupId);

        // A soft delete, never a DELETE statement: the row stays and is hidden
        // by the entity's global query filter. The two stamps are the point of
        // doing it this way - without them the row is indistinguishable from one
        // that was deleted by accident years ago.
        setup.IsDelete = true;
        setup.DeleteDate = _dateTimeProvider.UtcNow;
        setup.DeletedBy = request.DeletedBy;

        await _adminSetupRepository.UpdateAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet setup {SetupId} soft-deleted by user {DeletedBy}.",
            setup.SetupId,
            request.DeletedBy);
    }

    private async Task<AdminSetupResponse> AddNewAsync(
        AdminSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        await RequireSingleSetupPerUserAsync(request.UserId, null, cancellationToken);

        var setup = new TimesheetMasterSetup();
        request.ApplyTo(setup);

        // Written explicitly rather than by AuditableEntityInterceptor:
        // TimesheetMasterSetup does not derive from AuditableEntity, because
        // this table spells its soft-delete column IsDelete (a nullable bit)
        // where dbo.TimeLog spells it IsDeleted (a non-null int). The
        // interceptor therefore never sees this entity.
        setup.IsDelete = false;
        setup.CreateDate = _dateTimeProvider.UtcNow;
        setup.CreatedBy = request.PerformedBy;

        var saved = await _adminSetupRepository.AddAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet setup {SetupId} created for user {UserId} by user {PerformedBy}.",
            saved.SetupId,
            request.UserId,
            request.PerformedBy);

        return saved.ToResponse();
    }

    private async Task<AdminSetupResponse> UpdateExistingAsync(
        int setupId,
        AdminSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var setup = await _adminSetupRepository.GetForUpdateAsync(setupId, cancellationToken)
            ?? throw NotFoundException.For("Timesheet setup", setupId);

        // Checked on the edit path too, not just the insert: an edit can move a
        // setup to a different UserId, and that user may already have one.
        await RequireSingleSetupPerUserAsync(request.UserId, setupId, cancellationToken);

        request.ApplyTo(setup);

        setup.UpdateDate = _dateTimeProvider.UtcNow;
        setup.UpdatedBy = request.PerformedBy;

        await _adminSetupRepository.UpdateAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet setup {SetupId} updated by user {PerformedBy}.",
            setupId,
            request.PerformedBy);

        return setup.ToResponse();
    }

    /// <summary>
    /// Enforces one live setup per user.
    /// <para>
    /// Nothing in the database enforces this - there is no unique index on
    /// <c>UserID</c> - so it is enforced here, on the only path that writes the
    /// table. It matters because the timesheet screen reads its limits through
    /// <c>spc_GetTimesheetMasterSetupByUserID</c>, which does not guarantee
    /// uniqueness and simply takes the first row it gets: a second row would
    /// silently decide which limits apply.
    /// </para>
    /// <para>
    /// A conflict rather than a validation error, because the payload is fine -
    /// it is the current state of the data that makes the operation impossible.
    /// Editing the user's existing setup is the way to change it.
    /// </para>
    /// </summary>
    private async Task RequireSingleSetupPerUserAsync(
        int userId,
        int? excludingSetupId,
        CancellationToken cancellationToken)
    {
        var existing = await _adminSetupRepository.CountForUserAsync(
            userId,
            excludingSetupId,
            cancellationToken);

        if (existing > 0)
        {
            throw new ConflictException(
                $"User '{userId}' already has a timesheet setup. " +
                "A user can have only one, so edit the existing setup instead of adding another.");
        }
    }
}
