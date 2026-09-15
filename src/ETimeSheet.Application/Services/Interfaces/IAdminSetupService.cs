using ETimeSheet.Application.DTOs.AdminSetups;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the AdminSetup module - administrative CRUD over
/// <c>dbo.TimesheetMasterSetup</c>. Every public method of
/// <c>AdminSetupService</c> is declared here; its private helpers are not.
/// <para>
/// <b>A user has at most one setup.</b> That rule shapes this whole contract:
/// the read is by user id and returns a single setup, and a save that would
/// give a user a second one is rejected.
/// </para>
/// <para>
/// This is the only surface <c>AdminSetupController</c> is allowed to touch.
/// </para>
/// </summary>
public interface IAdminSetupService
{
    /// <summary>
    /// Adds or edits a user's timesheet setup. <b>One method for both</b>: the
    /// request's <c>SetupId</c> decides which - absent means insert, present
    /// means update of that row.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// A <c>SetupId</c> was supplied but no live row has it, which surfaces as a 404.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The save would leave the user with more than one setup, which surfaces as a 409.
    /// </exception>
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
