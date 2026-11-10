using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for the Scheduler module - <c>dbo.SchedulerConfiguration</c>,
/// and what the jobs read: the setups, users and time logs behind a reminder.
/// Every public operation of <c>SchedulerRepository</c> is declared here. The
/// jobs read and write <c>dbo.EmailQueue</c> through <see cref="IEmailQueueRepository"/>.
/// </summary>
public interface ISchedulerRepository
{
    /// <summary>
    /// Returns the rows whose <c>SchedulerConfigurationID</c> is in
    /// <paramref name="ids"/>, untracked, enabled or not. An id with no row is
    /// simply absent from the result.
    /// </summary>
    Task<IReadOnlyList<SchedulerConfiguration>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns everyone who could be due a reminder: one live setup per user
    /// (the lowest <c>SetupID</c>) with <c>NeedToSendReminder = 1</c> and a
    /// <c>TimeEntryLockAt</c>, whose user in <c>dbo.Signup</c> is not deleted.
    /// Untracked.
    /// </summary>
    Task<IReadOnlyList<ReminderCandidate>> GetReminderCandidatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what a reminder email says about each of
    /// <paramref name="userIds"/>: name, company, and their live setup (the
    /// lowest <c>SetupID</c>). A user with no live setup, or whose user row is
    /// deleted, is absent. Untracked.
    /// </summary>
    Task<IReadOnlyList<ReminderEmailDetails>> GetReminderEmailDetailsAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live, complete <c>dbo.TimeLog</c> entries of
    /// <paramref name="userIds"/> that touch any day from
    /// <paramref name="fromDate"/> to <paramref name="toDate"/> inclusive -
    /// drafts included. Untracked.
    /// </summary>
    Task<IReadOnlyList<LoggedSpan>> GetLoggedSpansAsync(
        IReadOnlyCollection<int> userIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the start dates of <paramref name="userIds"/>' live entries
    /// dated <paramref name="fromDate"/> to <paramref name="toDate"/> inclusive
    /// whose <c>SheetCode</c> has a row in <c>dbo.TimesheetSubmission</c> -
    /// whatever its status. Untracked.
    /// </summary>
    Task<IReadOnlyList<SubmittedEntry>> GetSubmittedEntriesAsync(
        IReadOnlyCollection<int> userIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every project with a live timesheet approver
    /// (<c>dbo.Timesheet_Approver_setup</c>, <c>IsTimesheetApprover = 1</c>)
    /// whose user is live, and whose project has a start date and a known
    /// submission frequency - one row per approver and project; a project has
    /// one approver. Untracked.
    /// </summary>
    Task<IReadOnlyList<ApproverReminderProject>> GetApproverReminderProjectsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live <c>dbo.TimesheetProjectTimeLog</c> hours of
    /// <paramref name="projectIds"/> dated <paramref name="fromDate"/> to
    /// <paramref name="toDate"/> inclusive, summed per project, user, day and
    /// sheet code. Untracked.
    /// </summary>
    Task<IReadOnlyList<ProjectLoggedDay>> GetProjectLoggedDaysAsync(
        IReadOnlyCollection<int> projectIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live <c>dbo.TimesheetProjectSubmission</c> rows of
    /// <paramref name="projectIds"/> with status <paramref name="statusId"/>
    /// whose period starts <paramref name="fromDate"/> to
    /// <paramref name="toDate"/> inclusive. Untracked.
    /// </summary>
    Task<IReadOnlyList<ProjectSheetSubmissionRow>> GetProjectSubmissionsAsync(
        IReadOnlyCollection<int> projectIds,
        int statusId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <c>dbo.Signup.Name</c> of each of <paramref name="userIds"/>,
    /// deleted users included - a user deleted since logging time is still
    /// named in what they logged. Untracked.
    /// </summary>
    Task<IReadOnlyList<UserName>> GetUserNamesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every live <c>dbo.TimesheetProjectSubmission</c> still
    /// <c>Submitted</c> whose project has an <c>AutoApproveInDays</c> greater
    /// than 0 - the sheets the auto-approve job may be due to approve.
    /// Untracked: whether one is actually due is decided by
    /// <c>SchedulerService</c>, and the rows it decides on are re-read tracked.
    /// </summary>
    Task<IReadOnlyList<AutoApproveCandidate>> GetAutoApproveCandidatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <c>dbo.TimesheetProjectSubmission</c> rows named by
    /// <paramref name="submissionIds"/>, <b>tracked</b> for update. A deleted
    /// row, or an id that names none, is simply absent.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectSubmission>> GetProjectSubmissionsForUpdateAsync(
        IReadOnlyCollection<int> submissionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the status changes already made to tracked sheets, together with
    /// <paramref name="auditLogs"/>, in <b>one</b> transaction - every sheet
    /// and its audit row commit together or not at all.
    /// </summary>
    Task SaveProjectSubmissionStatusesAsync(
        IReadOnlyCollection<TimesheetProjectSubmissionAuditLog> auditLogs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the working week and daily time of each of
    /// <paramref name="userIds"/>: their live <c>dbo.TimesheetMasterSetup</c>,
    /// the lowest <c>SetupID</c> when there are several. A user with no live
    /// setup is absent. Untracked.
    /// </summary>
    Task<IReadOnlyList<WorkingWeekSetup>> GetWorkingWeekSetupsAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);
}
