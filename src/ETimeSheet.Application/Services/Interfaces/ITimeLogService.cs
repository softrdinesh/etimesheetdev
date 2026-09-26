using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the TimeLog module. Every public method of
/// <c>TimeLogService</c> is declared here; its private helpers are not.
/// <para>
/// This is the only surface <c>TimeLogController</c> is allowed to touch.
/// </para>
/// </summary>
public interface ITimeLogService
{
    /// <summary>
    /// Returns every live entry one user has logged, across all tasks and all
    /// dates, newest first, via the <c>spc_GetTimeLoggedDetailsForTask</c>
    /// stored procedure. A user with no entries gets an empty list.
    /// </summary>
    Task<IReadOnlyCollection<TimeLoggedDetailResponse>> GetLoggedTimeListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timesheet setup that applies to one user - their maximum
    /// loggable time, contract type and the bounds of their timesheet week - via
    /// the <c>spc_GetTimesheetMasterSetupByUserID</c> stored procedure, or
    /// <see langword="null"/> when the user has no setup row.
    /// <para>
    /// <b>Null is an answer here, not a failure.</b> "This user has not been set
    /// up" is a true, useful statement about the database, and the caller asked
    /// a question that has been answered - so it reaches the client as a 200
    /// with <c>success: true</c> and <c>data: null</c>, not as a 404. Nothing
    /// went wrong: the request was well formed, it was authorised, it ran, and
    /// the answer is that there is no row.
    /// </para>
    /// <para>
    /// This is <i>not</i> the null-as-failure that CLAUDE.md §6 forbids. That
    /// rule is about signalling an <b>error</b> by returning null instead of
    /// throwing, and every error this method can hit still throws. Null means
    /// exactly one thing and it is a fact, not a problem.
    /// </para>
    /// <para>
    /// The write path is unaffected and still refuses a user with no setup:
    /// <see cref="SaveTimeLogAsync"/> reads the same procedure itself rather
    /// than through this method, because for a write the absence of a setup
    /// genuinely is a reason not to proceed.
    /// </para>
    /// </summary>
    Task<TimesheetMasterSetupResponse?> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the tasks one user owns, as two separate lists - project tasks
    /// and sprint tasks - via the <c>spc_GetUsersTaskList</c> stored procedure.
    /// A user who owns none gets two empty lists, not an error.
    /// </summary>
    Task<UserTaskListResponse> GetUserTaskListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one block of work for an employee - or, when the request names an
    /// existing entry in <c>TimeLogId</c>, overwrites that entry - after checking
    /// it against the timesheet setup that applies to them.
    /// <para>
    /// An edit faces every rule an add does, and one more: the entry as it
    /// stands must itself still be open. Before the day's cut-off every entry
    /// can be changed; after it, one that starts before the cut-off cannot -
    /// neither in place nor by moving it later.
    /// </para>
    /// <para>
    /// The setup drives four of the rules - the working week, whether
    /// back-dating is still open, the cut-off time for it and the daily maximum -
    /// so a user with no setup row cannot log time at all. The remaining rule,
    /// that a new entry may not overlap one the user already has that day, comes
    /// from the entries themselves.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The entry breaks one of the setup's rules, which surfaces as a 400.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The entry overlaps one the user already has that day, which surfaces as a 409.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// <c>TimeLogId</c> names no live entry, which surfaces as a 404.
    /// </exception>
    Task<TimeLogResponse> SaveTimeLogAsync(
        TimeLogSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one user's dashboard summary - logged time today and this
    /// timesheet week, the week's expected time, the percentage logged and the
    /// time still pending - via <c>spc_GetUserDashboardSummaryByUserID</c>, or
    /// <see langword="null"/> when the user does not exist or is deleted.
    /// <para>
    /// Null is an answer, as on <see cref="GetTimesheetMasterSetupByUserIdAsync"/>:
    /// it reaches the client as a 200 with <c>data: null</c>.
    /// </para>
    /// </summary>
    Task<UserDashboardSummaryResponse?> GetUserDashboardSummaryByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
