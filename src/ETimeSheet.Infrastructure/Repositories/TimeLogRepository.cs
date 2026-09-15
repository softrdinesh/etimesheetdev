using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Results;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the TimeLog module.
/// <para>
/// Each method executes the stored procedure of the same name on
/// <see cref="Context"/>. It answers data questions only - no permission
/// checks, no status rules and no decisions about what should happen; those
/// live in <c>TimeLogService</c>.
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
}
