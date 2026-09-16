using ETimeSheet.Application.DTOs.AdminSetups;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the AdminSetup module - administrative CRUD over
/// <c>dbo.TimesheetMasterSetup</c>. Every public method of
/// <c>AdminSetupService</c> is declared here; its private helpers are not.
/// <para>
/// <b>A user has exactly one setup.</b> That rule shapes this whole contract:
/// the read is by user id and returns a single setup, and the save takes no
/// setup id at all - there is only ever one row it could mean.
/// </para>
/// <para>
/// This is the only surface <c>AdminSetupController</c> is allowed to touch.
/// </para>
/// </summary>
public interface IAdminSetupService
{
    /// <summary>
    /// Saves a user's timesheet setup. <b>One method for insert and update</b>,
    /// and the caller does not choose between them: the user's live setup is
    /// updated if they have one, their deleted setup is overwritten and revived
    /// if they have one of those, and only a user with neither gets a new row.
    /// <para>
    /// It follows that this never fails for "not found" or "already exists" -
    /// every user is savable, and none of them can end up with two setups.
    /// </para>
    /// </summary>
    Task<AdminSetupResponse> SaveAsync(
        AdminSetupSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the timesheet setup belonging to one user.</summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The user has no setup - including the case where theirs was soft-deleted.
    /// </exception>
    Task<AdminSetupResponse> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a timesheet setup: the row stays, marked deleted and stamped
    /// with who did it and when.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live row has that id - deleting an already-deleted setup is a 404, not
    /// a silent success.
    /// </exception>
    Task DeleteAsync(
        AdminSetupDeleteRequest request,
        CancellationToken cancellationToken = default);
}
