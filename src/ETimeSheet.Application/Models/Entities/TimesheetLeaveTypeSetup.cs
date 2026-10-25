namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A leave type of one organisation, mapped onto the existing
/// <c>dbo.Timesheet_Leavetype_Setup</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// Mapped for the soft delete only: the list and the add/edit go through
/// <c>spc_GetTimesheetLeaveSetup</c> and
/// <c>spc_AddEditTimesheetLeaveSetup</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, <c>OrgID</c>, and a nullable <c>bit</c>
/// <c>IsDelete</c> rather than the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetLeaveTypeSetup
{
    /// <summary>The <c>LeaveTypeID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int LeaveTypeId { get; set; }

    public string? LeaveType { get; set; }

    /// <summary>The <c>NoofLeaveAllocated</c> column - <c>decimal(4,2)</c>.</summary>
    public decimal? NoOfLeaveAllocated { get; set; }

    /// <summary>The <c>OrgID</c> column.</summary>
    public int? OrganizationId { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }
}
