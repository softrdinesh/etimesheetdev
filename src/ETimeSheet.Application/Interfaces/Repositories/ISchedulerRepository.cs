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
}
