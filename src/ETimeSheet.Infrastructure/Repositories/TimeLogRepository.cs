using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Infrastructure.Data;
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

    public async Task<IReadOnlyList<TimeLoggedDetail>> GetTimeLoggedDetailsForTaskAsync(
        int userId,
        int taskId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default) =>
        // @PEndData is spelled exactly as the procedure declares it - the
        // misspelling is in the database, and the parameter name must match.
        await _db.spc_GetTimeLoggedDetailsForTask
            .FromSqlInterpolated(
                $@"EXEC dbo.spc_GetTimeLoggedDetailsForTask
                       @PUserID = {userId},
                       @PTaskID = {taskId},
                       @PStartDate = {startDate},
                       @PEndData = {endDate}")
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetMasterSetupDetail>> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _db.spc_GetTimesheetMasterSetupByUserID
            .FromSqlInterpolated(
                $"EXEC dbo.spc_GetTimesheetMasterSetupByUserID @PUserID = {userId}")
            .ToListAsync(cancellationToken);

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
}
