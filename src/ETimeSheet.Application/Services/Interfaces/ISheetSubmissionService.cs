using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the SheetSubmission module - timesheet submission
/// over <c>dbo.TimesheetSubmission</c>. Every public method of
/// <c>SheetSubmissionService</c> is declared here; its private helpers are not.
/// <para>
/// This is the only surface <c>SheetSubmissionController</c> is allowed to touch.
/// </para>
/// </summary>
public interface ISheetSubmissionService
{
    /// <summary>
    /// Submits one user's sheet: totals their saved <c>dbo.TimeLog</c> entries on
    /// it, records the submission in <c>dbo.TimesheetSubmission</c> with status
    /// 1 (Submitted), and logs that in <c>dbo.TimeSheetSubmissionAuditLog</c>.
    /// <para>
    /// Only <b>saved</b> entries count; drafts and deleted entries are left out.
    /// A sheet is submitted <b>once</b>.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The user has no saved time logged on the sheet - nothing to submit.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The sheet has already been submitted.
    /// </exception>
    Task<SheetSubmissionResponse> SubmitAsync(
        SheetSubmissionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a submitted sheet: sets its <c>StatusID</c>, stamps
    /// <c>ApprovedBy</c> / <c>ApprovedDate</c> or <c>RejectedBy</c> /
    /// <c>RejectedDate</c> with the admin and the time, and logs the change in
    /// <c>dbo.TimeSheetSubmissionAuditLog</c> - in one transaction.
    /// <para>
    /// A sheet can be reviewed <b>any number of times</b> - rejected, then
    /// approved later, or the reverse. The row carries only the latest
    /// decision (the opposite By / Date pair is cleared); the audit log keeps
    /// every one of them.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No submission exists for the sheet code.
    /// </exception>
    Task<SheetSubmissionResponse> ReviewAsync(
        SheetSubmissionReviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user's current timesheet week - dates, sheet code, and hours
    /// worked, expected and drift - as their admin setup defines it.
    /// <para>
    /// "Current" is today in the time zone the setup names, falling back to
    /// UTC, so the week is the same one the time log save puts today's entries
    /// in. A user with no setup still gets an answer: a Monday week, with the
    /// expected and drift figures null.
    /// </para>
    /// </summary>
    Task<CurrentWeekSheetDetailResponse> GetCurrentWeekSheetDetailsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the submitted sheets in one organisation, newest first - every
    /// user's when <c>UserId</c> is <c>0</c>, that user's otherwise. An
    /// organisation with nothing submitted is an empty list, not an error.
    /// <para>
    /// <c>AdminId</c> is passed through to the procedure, whose admin check is
    /// still a placeholder: <b>nothing restricts who may call this yet.</b>
    /// </para>
    /// </summary>
    Task<IReadOnlyCollection<SubmittedSheetResponse>> GetSubmittedSheetListAsync(
        SubmittedSheetListRequest request,
        CancellationToken cancellationToken = default);
}
