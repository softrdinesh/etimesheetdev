using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="TimeLog"/>. Every public operation of
/// <c>TimeLogRepository</c> is declared here; private query helpers are not.
/// <para>
/// The procedure reads execute stored procedures; the write path uses the entity and
/// the change tracker. That split is the database's, not a preference: the
/// procedures exist and are the agreed read contract, and there is no procedure
/// for the insert.
/// </para>
/// <para>
/// This interface answers only "what data" questions. It never answers
/// "is the caller allowed to" - that belongs to the service and authorisation layers.
/// </para>
/// </summary>
public interface ITimeLogRepository
{
    /// <summary>
    /// Returns every live entry one user has logged, newest first, by executing
    /// <c>dbo.spc_GetTimeLoggedDetailsForTask</c>.
    /// </summary>
    Task<IReadOnlyList<TimeLoggedDetail>> GetTimeLoggedDetailsByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timesheet setup rows held for one user, by executing
    /// <c>dbo.spc_GetTimesheetMasterSetupByUserID</c>.
    /// <para>
    /// A list rather than a single row, because the procedure filters by
    /// <c>UserID</c> without guaranteeing uniqueness. Deciding what "no rows" or
    /// "more than one row" means is the service's job, not this one's.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TimesheetMasterSetupDetail>> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the project tasks and the sprint tasks one user owns, by
    /// executing <c>dbo.spc_GetUsersTaskList</c> and reading both of its result
    /// sets.
    /// </summary>
    Task<UserTaskList> GetUserTaskListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one user's dashboard figures by executing
    /// <c>dbo.spc_GetUserDashboardSummaryByUserID</c>, or
    /// <see langword="null"/> when the procedure returns no row - the user is
    /// not in <c>dbo.Signup</c>, or is deleted there.
    /// </summary>
    Task<UserDashboardSummaryDetail?> GetUserDashboardSummaryByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live entries one user already has on one calendar day,
    /// untracked.
    /// <para>
    /// Matched on <c>StartDate</c>, which is the day the work is logged
    /// <b>against</b>; an entry that runs past midnight belongs to the day it
    /// started, not to both. Soft-deleted rows are excluded by the entity's
    /// global query filter, so a deleted entry never blocks a new one.
    /// </para>
    /// <para>
    /// One query serves two rules in the service - the daily maximum and the
    /// overlap check - rather than each of them going to the database
    /// separately.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TimeLog>> GetForUserOnDateAsync(
        int userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the highest generated sheet code in the table, or
    /// <see langword="null"/> when none has ever been generated.
    /// <para>
    /// "Highest" is <b>longest first, then greatest</b>, because the codes grow
    /// a digit when a width runs out: <c>SHT9999</c> is followed by <c>SHT00001</c>,
    /// and a plain string comparison would call <c>SHT9999</c> the larger of the
    /// two forever. Within one width the codes are zero-padded, so ordering them
    /// as text and as numbers is the same thing.
    /// </para>
    /// <para>
    /// Only codes of the generated shape - <c>SHT</c> followed by digits and
    /// nothing else - are considered. The table already holds hand-entered
    /// references such as <c>TS-00121</c>, and those name no position in the
    /// sequence.
    /// </para>
    /// <para>
    /// <b>Soft-deleted rows are included deliberately.</b> A deleted entry has
    /// still spent its code, and a caller who could not see it would hand the
    /// same code to a second row.
    /// </para>
    /// </summary>
    Task<string?> GetLatestSheetCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sheet code a user's entries already carry for the dates
    /// <paramref name="from"/> to <paramref name="to"/> inclusive - one
    /// timesheet week - or <see langword="null"/> when none of their entries in
    /// that range has a code yet.
    /// <para>
    /// The earliest entry's code wins (lowest <c>SheetID</c>), so the answer is
    /// stable even for a week logged before codes were shared and holding
    /// several.
    /// </para>
    /// <para>
    /// <b>Soft-deleted rows are included deliberately.</b> A week whose first
    /// entry was deleted keeps the code it was given, rather than the next entry
    /// opening a second code for the same week.
    /// </para>
    /// <para>
    /// <paramref name="excludeSheetId"/> leaves one entry out - the one being
    /// edited - so an entry moved into another week does not find its own old
    /// code there.
    /// </para>
    /// </summary>
    Task<string?> GetSheetCodeForUserBetweenAsync(
        int userId,
        DateTime from,
        DateTime to,
        int? excludeSheetId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new entry and saves, returning the same instance with its
    /// database-generated <c>SheetID</c> populated.
    /// </summary>
    Task<TimeLog> AddAsync(
        TimeLog timeLog,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live entry, <b>tracked</b>, so that changes made to it are
    /// written by <see cref="UpdateAsync"/>; or <see langword="null"/> when there
    /// is no such entry. A soft-deleted entry counts as absent.
    /// </summary>
    Task<TimeLog?> GetForUpdateAsync(
        int sheetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to an entry obtained from
    /// <see cref="GetForUpdateAsync"/>, returning the same instance.
    /// </summary>
    Task<TimeLog> UpdateAsync(
        TimeLog timeLog,
        CancellationToken cancellationToken = default);
}
