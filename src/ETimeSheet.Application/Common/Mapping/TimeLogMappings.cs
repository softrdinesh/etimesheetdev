using ETimeSheet.Application.DTOs.TimeLogs;
using ETimeSheet.Application.Models.Results;

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
        CanUserLoggedPreDayTime = setup.CanUserLoggedPreDayTime
    };
}
