namespace ETimeSheet.Application.Models.Results;

/// <summary>
/// One row of the result set returned by
/// <c>dbo.spc_GetTimesheetMasterSetupByUserID</c> - the limits and working-week
/// settings that apply to one user's timesheets.
/// <para>
/// This is a keyless type: it is not a table, it has no identity and it is never
/// tracked or written. It exists solely to give the procedure's SELECT list a
/// shape EF Core can materialise, which is why it carries only the six columns
/// the procedure returns. The full table is
/// <see cref="ETimeSheet.Application.Models.Entities.TimesheetMasterSetup"/>.
/// </para>
/// </summary>
public class TimesheetMasterSetupDetail
{
    public int SetupId { get; set; }

    /// <summary>
    /// The <c>MaxTimeinhrs</c> column, aliased by the procedure. A
    /// <c>time(7)</c>, not a number - so "8 hours" is stored as <c>08:00:00</c>.
    /// </summary>
    public TimeSpan? MaxTimeLoggedByUserInHours { get; set; }

    /// <summary>The <c>MaxTiminmins</c> column, aliased by the procedure. Also a <c>time(7)</c>.</summary>
    public TimeSpan? MaxTimeLoggedByUserInMinutes { get; set; }

    public int? ContractType { get; set; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c> (1 = Monday
    /// ... 7 = Sunday). The procedure selects the column straight through, so it
    /// returns whatever type the column has: an <c>int</c> since 2026-09-17.
    /// </summary>
    public int? StartDay { get; set; }

    /// <summary>Last day of the timesheet week. A <c>DayMaster.DayID</c>, as <see cref="StartDay"/>.</summary>
    public int? EndDay { get; set; }

    /// <summary>
    /// Whether the user may log time against a previous day. Stored as 0 or 1.
    /// <para>
    /// Exposed as a <c>bool?</c> rather than a number so callers get
    /// <c>true</c>/<c>false</c> instead of having to know that 1 means yes. The
    /// store type is handled in the mapping, not here.
    /// </para>
    /// </summary>
    public bool? CanUserLoggedPreDayTime { get; set; }
}
