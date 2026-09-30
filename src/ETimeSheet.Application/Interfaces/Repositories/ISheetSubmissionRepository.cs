using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for the SheetSubmission module - the
/// <c>dbo.TimesheetSubmission</c> table, its <c>dbo.TimeSheetSubmissionAuditLog</c>
/// trail, and the <c>dbo.TimeLog</c> entries a submission totals. Every public operation of
/// <c>SheetSubmissionRepository</c> is declared here.
/// </summary>
public interface ISheetSubmissionRepository
{
    /// <summary>
    /// Whether a submission already exists for <paramref name="sheetCode"/>.
    /// </summary>
    Task<bool> ExistsAsync(
        string sheetCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total minutes <paramref name="userId"/> has logged on
    /// <paramref name="sheetCode"/>, counting <b>saved</b> entries only, or
    /// <see langword="null"/> when they have no saved entry on it.
    /// <para>
    /// Drafts and soft-deleted entries are excluded, and so is an entry missing
    /// any of its four date/time values - the same entries
    /// <c>spc_GetTimeLoggedDetailsForTask</c> gives a null duration. Each entry
    /// is measured from start date + start time to end date + end time, so a
    /// shift that runs past midnight counts in full.
    /// </para>
    /// </summary>
    Task<int?> GetSavedMinutesAsync(
        string sheetCode,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new submission and saves, returning the same instance.
    /// <para>
    /// Anything staged with <see cref="AddAuditLog"/> beforehand is written by
    /// the same save, in the same transaction.
    /// </para>
    /// </summary>
    Task<TimesheetSubmission> AddAsync(
        TimesheetSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the submission for <paramref name="sheetCode"/>, <b>tracked</b>,
    /// so that changes made to it are written by <see cref="UpdateAsync"/>; or
    /// <see langword="null"/> when the sheet has not been submitted.
    /// </summary>
    Task<TimesheetSubmission?> GetForUpdateAsync(
        string sheetCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to a submission obtained from
    /// <see cref="GetForUpdateAsync"/>.
    /// <para>
    /// Anything staged with <see cref="AddAuditLog"/> beforehand is written by
    /// the same save, in the same transaction.
    /// </para>
    /// </summary>
    Task UpdateAsync(
        TimesheetSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages one <c>dbo.TimeSheetSubmissionAuditLog</c> entry. <b>It does not
    /// save.</b> The entry is written by the next write method on this
    /// repository - <see cref="AddAsync"/> or <see cref="UpdateAsync"/>, and
    /// every future one that changes a submission's status.
    /// <para>
    /// Staged rather than saved on its own so that a status change and its
    /// audit entry go to the database in one <c>SaveChanges</c>, and so one
    /// transaction: a change is never written without its audit entry, nor an
    /// audit entry without its change. A separate transaction would have to be
    /// wrapped in the retry execution strategy that <c>EnableRetryOnFailure</c>
    /// turns on; one save needs none of that.
    /// </para>
    /// </summary>
    void AddAuditLog(TimeSheetSubmissionAuditLog auditLog);

    /// <summary>
    /// Runs <c>dbo.spc_GetSubmittedSheetList</c>: the submitted sheets in
    /// <paramref name="orgId"/>, newest first - every user's when
    /// <paramref name="userId"/> is <c>0</c>, that user's otherwise.
    /// </summary>
    Task<IReadOnlyList<SubmittedSheetDetail>> GetSubmittedSheetListAsync(
        int adminId,
        int userId,
        int orgId,
        CancellationToken cancellationToken = default);
}
