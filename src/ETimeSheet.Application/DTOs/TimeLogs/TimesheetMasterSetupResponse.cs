namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// Caller-facing view of a user's timesheet setup, as returned by
/// <c>spc_GetTimesheetMasterSetupByUserID</c>.
/// </summary>
public class TimesheetMasterSetupResponse
{
    public int SetupId { get; init; }

    /// <summary>
    /// From the <c>MaxTimeinhrs</c> column, a <c>time(7)</c>. Serialises as
    /// <c>"08:00:00"</c>, not as a number.
    /// </summary>
    public TimeSpan? MaxTimeLoggedByUserInHours { get; init; }

    /// <summary>From the <c>MaxTiminmins</c> column, a <c>time(7)</c>.</summary>
    public TimeSpan? MaxTimeLoggedByUserInMinutes { get; init; }

    public int? ContractType { get; init; }

    /// <summary>First day of the timesheet week. A two-character code.</summary>
    public string? StartDay { get; init; }

    /// <summary>Last day of the timesheet week. A two-character code.</summary>
    public string? EndDay { get; init; }

    /// <summary>
    /// Whether the user may log time against a previous day. Held as 0 or 1 in
    /// the database and surfaced as <c>true</c>/<c>false</c>.
    /// </summary>
    public bool? CanUserLoggedPreDayTime { get; init; }
}
