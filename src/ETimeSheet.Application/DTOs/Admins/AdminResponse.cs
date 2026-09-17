namespace ETimeSheet.Application.DTOs.Admins;

/// <summary>
/// Caller-facing view of one <c>dbo.TimesheetMasterSetup</c> row.
/// <para>
/// This is the administrative view and carries the whole row, audit columns
/// included, because an admin screen has to show who last changed a setup.
/// It is not the same shape as
/// <c>ETimeSheet.Application.DTOs.TimeLogs.TimesheetMasterSetupResponse</c>,
/// which is the seven-column projection that
/// <c>spc_GetTimesheetMasterSetupByUserID</c> returns to the timesheet screen.
/// Two audiences, two contracts - deliberately not shared.
/// </para>
/// </summary>
public class AdminResponse
{
    public int SetupId { get; init; }

    public int? UserId { get; init; }

    /// <summary>The <c>MaxTimeinhrs</c> column. Serialises as <c>"08:00:00"</c>, not as a number.</summary>
    public TimeSpan? MaxTimeInHrs { get; init; }

    /// <summary>The <c>MaxTiminmins</c> column.</summary>
    public TimeSpan? MaxTimInMins { get; init; }

    public int? OrganizationId { get; init; }

    public int? ContractType { get; init; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c>: 1 =
    /// Monday through 7 = Sunday. Look the name up in <c>dbo.DayMaster</c>, or
    /// use <c>Constants.DayMaster</c>.
    /// </summary>
    public int? StartDay { get; init; }

    /// <summary>Last day of the timesheet week. A day id, as <see cref="StartDay"/>.</summary>
    public int? EndDay { get; init; }

    /// <summary>A day worked in addition to the normal week. A day id, as <see cref="StartDay"/>.</summary>
    public int? ExceptionDay { get; init; }

    public int? CountryId { get; init; }

    public TimeSpan? TimeEntryLockAt { get; init; }

    // ---- audit ----

    public int? CreatedBy { get; init; }

    public DateTime? CreateDate { get; init; }

    public int? UpdatedBy { get; init; }

    public DateTime? UpdateDate { get; init; }
}
