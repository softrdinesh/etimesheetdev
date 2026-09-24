using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written result-to-DTO projections.
/// <para>
/// Explicit mapping is preferred over a convention-based mapper here: it is
/// compile-time checked and trivially debuggable.
/// </para>
/// </summary>
internal static class TimeLogMappings
{
    internal static TimeLoggedDetailResponse ToResponse(this TimeLoggedDetail detail) => new()
    {
        SheetId = detail.SheetId,
        SheetCode = detail.SheetCode,
        TaskId = detail.TaskId,
        IsProjectTask = detail.IsProjectTask,
        Description = detail.Description,
        StartDate = detail.StartDate,
        StartTime = detail.StartTime,
        EndDate = detail.EndDate,
        EndTime = detail.EndTime,
        Status = detail.Status,
        // 1 = Save, 2 = Draft only, so this resolves to "Save" or "Draft" -
        // see Constants.TimeLog.Status. Null column stays an empty string
        // rather than being invented as a default status.
        StatusName = detail.Status?.ToString() ?? string.Empty,
        TotalWorkingHours = detail.TotalWorkingHours,
        TotalWorkingMinutes = detail.TotalWorkingMinutes
    };

    internal static IReadOnlyCollection<TimeLoggedDetailResponse> ToResponses(
        this IEnumerable<TimeLoggedDetail> details) =>
        details.Select(ToResponse).ToArray();

    internal static TimesheetMasterSetupResponse ToResponse(this TimesheetMasterSetupDetail setup) => new()
    {
        SetupId = setup.SetupId,
        MaxTimeLoggedByUserInHours = setup.MaxTimeLoggedByUserInHours,
        MaxTimeLoggedByUserInMinutes = setup.MaxTimeLoggedByUserInMinutes,
        ContractType = setup.ContractType,
        StartDay = setup.StartDay,
        EndDay = setup.EndDay,
        CanUserLoggedPreDayTime = setup.CanUserLoggedPreDayTime,
        CountryId = setup.CountryId,
        TimeZone = setup.TimeZone
    };

    internal static UserTaskResponse ToResponse(this UserTaskListItem task) => new()
    {
        TaskId = task.TaskId,
        TaskName = task.TaskName
    };

    internal static UserTaskListResponse ToResponse(this UserTaskList tasks) => new()
    {
        ProjectTasks = tasks.ProjectTasks.Select(ToResponse).ToArray(),
        SprintTasks = tasks.SprintTasks.Select(ToResponse).ToArray()
    };

    /// <summary>
    /// Projects a saved entry back to the caller.
    /// <para>
    /// <paramref name="totalWorkingHours"/> is passed in rather than computed
    /// here: how long an entry covers is a business calculation the service
    /// already had to perform to check it against the daily maximum, and working
    /// it out twice invites the two answers to disagree.
    /// </para>
    /// </summary>
    internal static TimeLogResponse ToResponse(this TimeLog timeLog, decimal totalWorkingHours) => new()
    {
        SheetId = timeLog.SheetId,
        SheetCode = timeLog.SheetCode,
        TaskId = timeLog.TaskId,
        IsProjectTask = timeLog.IsProjectTask,
        Description = timeLog.Description,
        UserId = timeLog.UserId,
        StartDate = timeLog.StartDate,
        StartTime = timeLog.StartTime,
        EndDate = timeLog.EndDate,
        EndTime = timeLog.EndTime,
        Status = timeLog.Status,
        StatusName = timeLog.Status?.ToString() ?? string.Empty,
        TotalWorkingHours = totalWorkingHours,
        CreatedBy = timeLog.CreatedBy,
        CreateDate = timeLog.CreateDate,
        UpdatedBy = timeLog.UpdatedBy,
        UpdateDate = timeLog.UpdateDate
    };
}
