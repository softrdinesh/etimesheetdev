namespace ETimeSheet.Application.DTOs.AdminSetups;

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
public class AdminSetupResponse
{
    public int SetupId { get; init; }

    public int? UserId { get; init; }

    /// <summary>The <c>MaxTimeinhrs</c> column. Serialises as <c>"08:00:00"</c>, not as a number.</summary>
    public TimeSpan? MaxTimeInHrs { get; init; }

    /// <summary>The <c>MaxTiminmins</c> column.</summary>
    public TimeSpan? MaxTimInMins { get; init; }

    public int? OrganizationId { get; init; }

    public int? ContractType { get; init; }

    /// <summary>First day of the timesheet week. Trailing blanks from the <c>char(2)</c> column are trimmed off.</summary>
    public string? StartDay { get; init; }

    /// <summary>Last day of the timesheet week.</summary>
    public string? EndDay { get; init; }

    /// <summary>A day treated as an exception to the normal week.</summary>
    public string? ExceptionDay { get; init; }

    public int? CountryId { get; init; }

    public TimeSpan? TimeEntryLockAt { get; init; }

    // ---- audit ----

    public int? CreatedBy { get; init; }

    public DateTime? CreateDate { get; init; }

    public int? UpdatedBy { get; init; }

    public DateTime? UpdateDate { get; init; }
}
