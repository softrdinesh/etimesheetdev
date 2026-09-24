using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Infrastructure.Data;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the TimeLog module.
/// <para>
/// The reads execute the stored procedure of the same name on
/// <see cref="Context"/>; the write path goes through the entity and the change
/// tracker, because no procedure exists for it. Either way this answers data
/// questions only - no permission checks, no status rules and no decisions about
/// what should happen; those live in <c>TimeLogService</c>.
/// </para>
/// </summary>
public class TimeLogRepository : ITimeLogRepository
{
    private readonly Context _db;

    public TimeLogRepository(Context db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TimeLoggedDetail>> GetTimeLoggedDetailsByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        // Not composed on: the procedure's ORDER BY CreateDate DESC is the
        // order, and wrapping the EXEC in a further query is not possible
        // anyway - SQL Server cannot select FROM a stored procedure.
        await _db.spc_GetTimeLoggedDetailsForTask
            .FromSqlInterpolated(
                $"EXEC dbo.spc_GetTimeLoggedDetailsForTask @PUserID = {userId}")
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetMasterSetupDetail>> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _db.spc_GetTimesheetMasterSetupByUserID
            .FromSqlInterpolated(
                $"EXEC dbo.spc_GetTimesheetMasterSetupByUserID @PUserID = {userId}")
            .ToListAsync(cancellationToken);

    public async Task<UserTaskList> GetUserTaskListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Not FromSqlInterpolated: EF Core reads only the first result set of a
        // batch, and this procedure returns two - the sprint tasks would be
        // dropped without a word. So the procedure runs on the context's own
        // connection and each result set is read by column name.
        //
        // Wrapped in the execution strategy so the retry policy the context is
        // configured with still covers it; a manual command would otherwise
        // bypass it.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var connection = _db.Database.GetDbConnection();
            await _db.Database.OpenConnectionAsync(cancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.spc_GetUsersTaskList";
                command.CommandType = CommandType.StoredProcedure;

                if (_db.Database.GetCommandTimeout() is { } timeout)
                {
                    command.CommandTimeout = timeout;
                }

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@pUserID";
                parameter.DbType = DbType.Int32;
                parameter.Value = userId;
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                // The procedure's order is the contract: project tasks first,
                // then sprint tasks.
                var projectTasks = await ReadTasksAsync(reader, cancellationToken);

                var sprintTasks = await reader.NextResultAsync(cancellationToken)
                    ? await ReadTasksAsync(reader, cancellationToken)
                    : Array.Empty<UserTaskListItem>();

                return new UserTaskList
                {
                    ProjectTasks = projectTasks,
                    SprintTasks = sprintTasks
                };
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        });
    }

    /// <summary>
    /// Reads the current result set of <c>spc_GetUsersTaskList</c>. Columns are
    /// looked up by name - <c>TaskID</c>, <c>Taskname</c> - so a reordered
    /// SELECT list still reads correctly; a renamed column fails loudly.
    /// </summary>
    private static async Task<IReadOnlyList<UserTaskListItem>> ReadTasksAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var taskIdOrdinal = reader.GetOrdinal("TaskID");
        var taskNameOrdinal = reader.GetOrdinal("Taskname");

        var tasks = new List<UserTaskListItem>();

        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(new UserTaskListItem
            {
                TaskId = reader.GetInt32(taskIdOrdinal),
                TaskName = await reader.IsDBNullAsync(taskNameOrdinal, cancellationToken)
                    ? null
                    : reader.GetString(taskNameOrdinal)
            });
        }

        return tasks;
    }

    public async Task<IReadOnlyList<TimeLog>> GetForUserOnDateAsync(
        int userId,
        DateTime date,
        CancellationToken cancellationToken = default) =>
        await _db.TimeLog
            .AsNoTracking()
            .Where(timeLog => timeLog.UserId == userId)
            // .Date on the parameter, not on the column: comparing
            // timeLog.StartDate.Value.Date would wrap the column in a CAST and
            // cost any index on it. The column is a date, so it is already at
            // midnight and an equality test is exact.
            .Where(timeLog => timeLog.StartDate == date.Date)
            .OrderBy(timeLog => timeLog.StartTime)
            .ToListAsync(cancellationToken);

    public async Task<string?> GetLatestSheetCodeAsync(
        CancellationToken cancellationToken = default) =>
        await _db.TimeLog
            .AsNoTracking()
            // A soft-deleted entry has still spent its code, so the filter is
            // ignored on purpose: skipping those rows would reissue a code that
            // is already sitting in the table, invisible but present.
            .IgnoreQueryFilters()
            // "T followed by digits and nothing else", evaluated in SQL Server
            // rather than in memory. The first pattern requires the T and at
            // least one digit; the second rejects anything non-numeric after the
            // T, which is what keeps hand-entered references like "TS-00121" out
            // of the sequence.
            .Where(timeLog => timeLog.SheetCode != null
                && EF.Functions.Like(timeLog.SheetCode, "T[0-9]%")
                && !EF.Functions.Like(timeLog.SheetCode.Substring(1), "%[^0-9]%"))
            // Longest first, then greatest. LEN() before the text comparison is
            // what makes T00001 outrank T9999 - see ITimeLogRepository.
            .OrderByDescending(timeLog => timeLog.SheetCode!.Length)
            .ThenByDescending(timeLog => timeLog.SheetCode)
            .Select(timeLog => timeLog.SheetCode)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimeLog> AddAsync(
        TimeLog timeLog,
        CancellationToken cancellationToken = default)
    {
        await _db.TimeLog.AddAsync(timeLog, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // SheetID is an identity column, so it is only populated after the save -
        // and it is the one thing the caller could not have known beforehand.
        return timeLog;
    }

    public async Task<TimeLog?> GetForUpdateAsync(
        int sheetId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The global query filter
        // still applies, so a soft-deleted entry cannot be edited back to life.
        await _db.TimeLog
            .FirstOrDefaultAsync(timeLog => timeLog.SheetId == sheetId, cancellationToken);

    public async Task<TimeLog> UpdateAsync(
        TimeLog timeLog,
        CancellationToken cancellationToken = default)
    {
        // The entity is already tracked, so only the columns that actually
        // changed are written. UpdateDate is stamped by the audit interceptor.
        await _db.SaveChangesAsync(cancellationToken);

        return timeLog;
    }
}
