using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// Caller-facing view of one row returned by
/// <c>spc_GetTimeLoggedDetailsForTask</c>.
/// </summary>
public class TimeLoggedDetailResponse
{
    public int SheetId { get; init; }

    public string? SheetCode { get; init; }

    public string? Description { get; init; }

    public DateTime? StartDate { get; init; }

    public TimeSpan? StartTime { get; init; }

    public DateTime? EndDate { get; init; }

    public TimeSpan? EndTime { get; init; }

    /// <summary>
    /// <b>1 = Save, 2 = Draft only</b> - see
    /// <see cref="ETimeSheet.Shared.Utilities.Constants.TimeLog.Status"/>.
    /// Serialised by name, so a caller sees "Save" or "Draft".
    /// </summary>
    public TimeLogStatus? Status { get; init; }

    /// <summary>"Save" or "Draft". Empty when the row's <c>Status</c> column is null.</summary>
    public string StatusName { get; init; } = string.Empty;

    /// <summary>Duration of this entry in hours, to two decimal places.</summary>
    public decimal? TotalWorkingHours { get; init; }

    /// <summary>Duration of this entry in whole minutes.</summary>
    public int? TotalWorkingMinutes { get; init; }
}
