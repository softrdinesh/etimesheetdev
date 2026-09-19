using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="TimeLog"/>. Every public operation of
/// <c>TimeLogRepository</c> is declared here; private query helpers are not.
/// <para>
/// The two reads execute stored procedures; the write path uses the entity and
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
    /// Returns the entries one user logged against one task, by executing
    /// <c>dbo.spc_GetTimeLoggedDetailsForTask</c>.
    /// </summary>
    Task<IReadOnlyList<TimeLoggedDetail>> GetTimeLoggedDetailsForTaskAsync(
        int userId,
        int taskId,
        DateTime startDate,
        DateTime endDate,
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
    /// a digit when a width runs out: <c>T9999</c> is followed by <c>T00001</c>,
    /// and a plain string comparison would call <c>T9999</c> the larger of the
    /// two forever. Within one width the codes are zero-padded, so ordering them
    /// as text and as numbers is the same thing.
    /// </para>
    /// <para>
    /// Only codes of the generated shape - <c>T</c> followed by digits and
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
    /// Inserts a new entry and saves, returning the same instance with its
    /// database-generated <c>SheetID</c> populated.
    /// </summary>
    Task<TimeLog> AddAsync(
        TimeLog timeLog,
        CancellationToken cancellationToken = default);
}
