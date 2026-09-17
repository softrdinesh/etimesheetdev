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

    /// <summary>
    /// First day of the timesheet week - a <c>DayMaster.DayID</c>: 1 = Monday
    /// through 7 = Sunday. It was a two-character code until 2026-09-17.
    /// </summary>
    public int? StartDay { get; init; }

    /// <summary>Last day of the timesheet week. A <c>DayMaster.DayID</c>, as <see cref="StartDay"/>.</summary>
    public int? EndDay { get; init; }

    /// <summary>
    /// Whether the user may log time against a previous day. Held as 0 or 1 in
    /// the database and surfaced as <c>true</c>/<c>false</c>.
    /// </summary>
    public bool? CanUserLoggedPreDayTime { get; init; }
}
