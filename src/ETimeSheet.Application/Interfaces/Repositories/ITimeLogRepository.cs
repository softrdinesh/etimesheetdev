using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models.Results;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="TimeLog"/>. Every public operation of
/// <c>TimeLogRepository</c> is declared here; private query helpers are not.
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
}
